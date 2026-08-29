#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace Authoritative.Services;

public record AgentTask(
    string Id,
    string Status,
    string Description,
    string? Result,
    string AgentLog,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public interface IAgentTaskService
{
    Task<AgentTask> SubmitTaskAsync(string description, CancellationToken ct = default);
    Task<IReadOnlyList<AgentTask>> GetTasksAsync(string? status, CancellationToken ct = default);
    Task<AgentTask?> GetTaskAsync(string taskId, CancellationToken ct = default);
    Task CancelTaskAsync(string taskId, CancellationToken ct = default);
}

public sealed class AgentTaskService : IAgentTaskService, IHostedService
{
    private static readonly SemaphoreSlim _concurrencyGate = new(3, 3);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AgentTaskService> _logger;

    private string _connectionString = string.Empty;
    private string _openAiApiKey = string.Empty;
    private string _model = "gpt-4o";
    private string _workspacePath = "/workspace";
    private bool _schemaReady;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    // ── Tools definition sent to the model ──────────────────────────────────
    private static readonly JArray _tools = JArray.Parse("""
    [
      {
        "type": "function",
        "function": {
          "name": "read_file",
          "description": "Read the contents of a file in the workspace. Returns file text.",
          "parameters": {
            "type": "object",
            "properties": {
              "path": { "type": "string", "description": "Path relative to workspace root" }
            },
            "required": ["path"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "write_file",
          "description": "Write or overwrite a file in the workspace. Creates parent directories if needed.",
          "parameters": {
            "type": "object",
            "properties": {
              "path":    { "type": "string", "description": "Path relative to workspace root" },
              "content": { "type": "string", "description": "Full file content to write" }
            },
            "required": ["path", "content"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "list_directory",
          "description": "List files and subdirectories at a given path in the workspace.",
          "parameters": {
            "type": "object",
            "properties": {
              "path": { "type": "string", "description": "Path relative to workspace root, or '.' for root" }
            },
            "required": ["path"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "run_shell",
          "description": "Run a shell command inside the container (30s timeout, cwd=workspace). Use for compiling, grepping, etc.",
          "parameters": {
            "type": "object",
            "properties": {
              "command": { "type": "string", "description": "Shell command to execute" }
            },
            "required": ["command"]
          }
        }
      },
      {
        "type": "function",
        "function": {
          "name": "search_code",
          "description": "Search for a text pattern across files in the workspace using grep.",
          "parameters": {
            "type": "object",
            "properties": {
              "pattern":   { "type": "string", "description": "Regex or literal pattern to search for" },
              "path":      { "type": "string", "description": "Relative path to search in, or '.' for all" },
              "file_glob": { "type": "string", "description": "Optional glob like '*.cs' or '*.tsx'" }
            },
            "required": ["pattern", "path"]
          }
        }
      }
    ]
    """);

    public AgentTaskService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<AgentTaskService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    // ── IHostedService ──────────────────────────────────────────────────────

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _connectionString = _config["POSTGRES_CONNECTION_STRING"]
            ?? "Host=postgres;Port=5432;Username=mmouser;Password=mmopass;Database=mmodb";
        _openAiApiKey = _config["OPENAI_API_KEY"] ?? string.Empty;
        _model = _config["AGENT_MODEL"] ?? "gpt-4o";
        _workspacePath = _config["WORKSPACE_PATH"] ?? "/workspace";

        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
        _logger.LogInformation(
            "AgentTaskService started. Model={Model} Workspace={Workspace} OpenAiConfigured={Configured}",
            _model, _workspacePath, !string.IsNullOrWhiteSpace(_openAiApiKey));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_workerTask != null)
            await Task.WhenAny(_workerTask, Task.Delay(5000, cancellationToken));
    }

    // ── Background worker loop ──────────────────────────────────────────────

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 10 && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                await InitSchemaAsync(ct);
                _schemaReady = true;
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Agent schema init attempt {A}/10 failed", attempt);
                await Task.Delay(3000, ct);
            }
        }

        if (!_schemaReady)
        {
            _logger.LogError("AgentTaskService: schema init failed after 10 attempts — worker exiting");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var task = await PickPendingTaskAsync(ct);
                if (task != null)
                {
                    await _concurrencyGate.WaitAsync(ct);
                    _ = Task.Run(async () =>
                    {
                        try { await ExecuteAgentTaskAsync(task, ct); }
                        catch (Exception ex) { _logger.LogError(ex, "Unhandled error in agent task {Id}", task.Id); }
                        finally { _concurrencyGate.Release(); }
                    }, CancellationToken.None);
                }
                else
                {
                    await Task.Delay(2000, ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent worker loop error");
                await Task.Delay(5000, ct);
            }
        }
    }

    // ── IAgentTaskService ───────────────────────────────────────────────────

    public async Task<AgentTask> SubmitTaskAsync(string description, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO agent_tasks (id, description)
              VALUES (@id, @desc)
              RETURNING id, status, description, result, agent_log,
                        created_at, updated_at, completed_at", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("desc", description);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return MapRow(reader);
    }

    public async Task<IReadOnlyList<AgentTask>> GetTasksAsync(string? status, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var sql = status is null
            ? "SELECT id, status, description, result, agent_log, created_at, updated_at, completed_at FROM agent_tasks ORDER BY created_at DESC LIMIT 100"
            : "SELECT id, status, description, result, agent_log, created_at, updated_at, completed_at FROM agent_tasks WHERE status = @s ORDER BY created_at DESC LIMIT 100";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (status is not null) cmd.Parameters.AddWithValue("s", status);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AgentTask>();
        while (await reader.ReadAsync(ct)) list.Add(MapRow(reader));
        return list;
    }

    public async Task<AgentTask?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, status, description, result, agent_log, created_at, updated_at, completed_at FROM agent_tasks WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", taskId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public async Task CancelTaskAsync(string taskId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE agent_tasks SET status = 'cancelled', updated_at = NOW() WHERE id = @id AND status IN ('pending', 'running')", conn);
        cmd.Parameters.AddWithValue("id", taskId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── GPT-4o agentic loop ─────────────────────────────────────────────────

    private async Task ExecuteAgentTaskAsync(AgentTask task, CancellationToken ct)
    {
        _logger.LogInformation("Agent starting task {Id}: {Desc}", task.Id, task.Description[..Math.Min(80, task.Description.Length)]);

        if (string.IsNullOrWhiteSpace(_openAiApiKey))
        {
            await FailTaskAsync(task.Id, "OPENAI_API_KEY is not configured on the server.", ct);
            return;
        }

        await AppendLogAsync(task.Id, $"[{DateTimeOffset.UtcNow:O}] Agent started\nTask: {task.Description}\n\n", ct);

        var systemPrompt = $"""
You are an autonomous coding agent running inside the DunGenMMORPGEngine Docker container.
Your workspace is mounted at /workspace.

Tech stack:
- /workspace/services/authoritative/     C# .NET 10 ASP.NET Core backend
- /workspace/services/admin-ui-zip/shims/ React TypeScript admin UI (Vite + Tailwind)
- /workspace/services/generator-service/  Node.js terrain generator
- /workspace/docker-compose.yml          all Docker services
- /workspace/ported-from-zip-unmodified/  original scaffolding (read-only reference)

Database: Postgres mmodb @ postgres:5432 (user: mmouser / pass: mmopass)
Redis: redis:6379

Use your tools to read the codebase, make changes, and verify them.
When done, summarize what was changed and why.
Current time: {DateTimeOffset.UtcNow:R}
""";

        var messages = new JArray
        {
            new JObject { ["role"] = "system", ["content"] = systemPrompt },
            new JObject { ["role"] = "user",   ["content"] = task.Description }
        };

        int maxIterations = 40;
        for (int i = 1; i <= maxIterations; i++)
        {
            // Check for cancellation between iterations
            var current = await GetTaskAsync(task.Id, ct);
            if (current?.Status == "cancelled")
            {
                await AppendLogAsync(task.Id, "\n[CANCELLED] Task was cancelled.\n", ct);
                return;
            }

            await AppendLogAsync(task.Id, $"\n--- Iteration {i} ---\n", ct);

            JObject response;
            try
            {
                response = await CallOpenAiAsync(messages, ct);
            }
            catch (Exception ex)
            {
                await FailTaskAsync(task.Id, $"OpenAI API error: {ex.Message}", ct);
                return;
            }

            var choice = response["choices"]?[0];
            var msg = choice?["message"] as JObject;
            if (msg == null)
            {
                await FailTaskAsync(task.Id, "Unexpected response shape from OpenAI API.", ct);
                return;
            }

            // Append assistant message to history
            messages.Add(msg);

            var toolCalls = msg["tool_calls"] as JArray;
            if (toolCalls != null && toolCalls.Count > 0)
            {
                foreach (var tc in toolCalls)
                {
                    var callId  = tc["id"]?.ToString() ?? string.Empty;
                    var fnName  = tc["function"]?["name"]?.ToString() ?? string.Empty;
                    var fnArgs  = tc["function"]?["arguments"]?.ToString() ?? "{}";

                    await AppendLogAsync(task.Id, $"[TOOL] {fnName}({TruncateForLog(fnArgs, 200)})\n", ct);

                    var result = await DispatchToolAsync(fnName, fnArgs, ct);
                    var truncatedResult = TruncateForLog(result, 500);
                    await AppendLogAsync(task.Id, $"[RESULT] {truncatedResult}\n", ct);

                    messages.Add(new JObject
                    {
                        ["role"]         = "tool",
                        ["tool_call_id"] = callId,
                        ["content"]      = result.Length > 8000 ? result[..8000] + "\n[truncated]" : result
                    });
                }
            }
            else
            {
                // No tool calls — model is done
                var finalText = msg["content"]?.ToString() ?? "(no output)";
                await AppendLogAsync(task.Id, $"\n[DONE]\n{finalText}\n", ct);
                await CompleteTaskAsync(task.Id, finalText, ct);
                return;
            }
        }

        await FailTaskAsync(task.Id, $"Reached maximum iterations ({maxIterations}) without finishing.", ct);
    }

    // ── OpenAI HTTP call ────────────────────────────────────────────────────

    private async Task<JObject> CallOpenAiAsync(JArray messages, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _openAiApiKey);
        client.Timeout = TimeSpan.FromSeconds(120);

        var body = new JObject
        {
            ["model"]       = _model,
            ["messages"]    = messages,
            ["tools"]       = _tools,
            ["tool_choice"] = "auto",
            ["max_tokens"]  = 4096
        };

        var content = new StringContent(
            body.ToString(Formatting.None),
            Encoding.UTF8,
            "application/json");

        var httpResp = await client.PostAsync("https://api.openai.com/v1/chat/completions", content, ct);
        var raw = await httpResp.Content.ReadAsStringAsync(ct);

        if (!httpResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)httpResp.StatusCode}: {raw[..Math.Min(300, raw.Length)]}");

        return JObject.Parse(raw);
    }

    // ── Tool dispatch ───────────────────────────────────────────────────────

    private async Task<string> DispatchToolAsync(string name, string argsJson, CancellationToken ct)
    {
        JObject args;
        try { args = JObject.Parse(argsJson); }
        catch { return $"Error: could not parse tool arguments: {argsJson}"; }

        try
        {
            return name switch
            {
                "read_file"      => ToolReadFile(args["path"]?.ToString() ?? string.Empty),
                "write_file"     => ToolWriteFile(args["path"]?.ToString() ?? string.Empty, args["content"]?.ToString() ?? string.Empty),
                "list_directory" => ToolListDirectory(args["path"]?.ToString() ?? "."),
                "run_shell"      => await ToolRunShellAsync(args["command"]?.ToString() ?? string.Empty, ct),
                "search_code"    => await ToolSearchCodeAsync(
                                        args["pattern"]?.ToString() ?? string.Empty,
                                        args["path"]?.ToString() ?? ".",
                                        args["file_glob"]?.ToString(),
                                        ct),
                _ => $"Unknown tool: {name}"
            };
        }
        catch (Exception ex)
        {
            return $"Tool error ({name}): {ex.Message}";
        }
    }

    private string ToolReadFile(string relativePath)
    {
        var target = ResolveSafePath(relativePath);
        if (target == null) return "Error: path traversal not allowed";
        if (!File.Exists(target)) return $"Error: file not found: {relativePath}";
        var text = File.ReadAllText(target, Encoding.UTF8);
        return text.Length > 20000 ? text[..20000] + $"\n[truncated — {text.Length} chars total]" : text;
    }

    private string ToolWriteFile(string relativePath, string content)
    {
        var target = ResolveSafePath(relativePath);
        if (target == null) return "Error: path traversal not allowed";
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, content, Encoding.UTF8);
        return $"Wrote {content.Length} chars to {relativePath}";
    }

    private string ToolListDirectory(string relativePath)
    {
        var target = ResolveSafePath(relativePath);
        if (target == null) return "Error: path traversal not allowed";
        if (!Directory.Exists(target)) return $"Error: directory not found: {relativePath}";
        var entries = Directory.GetFileSystemEntries(target)
            .Select(e => Path.GetRelativePath(target, e) + (Directory.Exists(e) ? "/" : ""))
            .OrderBy(e => e)
            .Take(500)
            .ToList();
        return entries.Count == 0 ? "(empty)" : string.Join("\n", entries);
    }

    private async Task<string> ToolRunShellAsync(string command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command)) return "Error: empty command";
        var psi = new ProcessStartInfo
        {
            FileName               = "/bin/sh",
            Arguments              = $"-c \"{command.Replace("\"", "\\\"")}\"",
            WorkingDirectory       = _workspacePath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };
        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return "Error: command timed out after 30s";
        }
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout + (string.IsNullOrWhiteSpace(stderr) ? "" : "\n[stderr]\n" + stderr);
        return string.IsNullOrWhiteSpace(output) ? "(no output)" : output.Length > 8000 ? output[..8000] + "\n[truncated]" : output;
    }

    private async Task<string> ToolSearchCodeAsync(string pattern, string relativePath, string? fileGlob, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return "Error: empty pattern";
        var globFlag = string.IsNullOrWhiteSpace(fileGlob) ? "" : $"--include=\"{fileGlob}\" ";
        var cmd = $"grep -r -n {globFlag}\"{pattern.Replace("\"", "\\\"")}\" \"{relativePath}\" 2>&1 | head -100";
        return await ToolRunShellAsync(cmd, ct);
    }

    // ── Postgres helpers ────────────────────────────────────────────────────

    private async Task InitSchemaAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS agent_tasks (
                id           TEXT        PRIMARY KEY,
                status       TEXT        NOT NULL DEFAULT 'pending',
                description  TEXT        NOT NULL,
                result       TEXT,
                agent_log    TEXT        NOT NULL DEFAULT '',
                created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                completed_at TIMESTAMPTZ
            );
            CREATE INDEX IF NOT EXISTS idx_agent_tasks_status ON agent_tasks (status, created_at DESC);
            """, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<AgentTask?> PickPendingTaskAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        // Atomic pick-and-mark-running using FOR UPDATE SKIP LOCKED
        await using var cmd = new NpgsqlCommand("""
            UPDATE agent_tasks SET status = 'running', updated_at = NOW()
            WHERE id = (
                SELECT id FROM agent_tasks
                WHERE status = 'pending'
                ORDER BY created_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, status, description, result, agent_log,
                      created_at, updated_at, completed_at
            """, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    private async Task AppendLogAsync(string taskId, string text, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE agent_tasks SET agent_log = agent_log || @text, updated_at = NOW() WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("text", text);
        cmd.Parameters.AddWithValue("id", taskId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task CompleteTaskAsync(string taskId, string result, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE agent_tasks SET status='done', result=@r, updated_at=NOW(), completed_at=NOW() WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("r", result);
        cmd.Parameters.AddWithValue("id", taskId);
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Agent task {Id} completed", taskId);
    }

    private async Task FailTaskAsync(string taskId, string reason, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE agent_tasks SET status='failed', result=@r, updated_at=NOW(), completed_at=NOW() WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("r", reason);
        cmd.Parameters.AddWithValue("id", taskId);
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogWarning("Agent task {Id} failed: {Reason}", taskId, reason);
    }

    // ── Utilities ───────────────────────────────────────────────────────────

    private string? ResolveSafePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var workspace = Path.GetFullPath(_workspacePath);
        var target    = Path.GetFullPath(Path.Combine(workspace, relativePath));
        return target.StartsWith(workspace, StringComparison.OrdinalIgnoreCase) ? target : null;
    }

    private static string TruncateForLog(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static AgentTask MapRow(NpgsqlDataReader reader) => new(
        Id:          reader.GetString(0),
        Status:      reader.GetString(1),
        Description: reader.GetString(2),
        Result:      reader.IsDBNull(3) ? null : reader.GetString(3),
        AgentLog:    reader.GetString(4),
        CreatedAt:   reader.GetFieldValue<DateTimeOffset>(5),
        UpdatedAt:   reader.GetFieldValue<DateTimeOffset>(6),
        CompletedAt: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
}

public sealed class AgentTaskSubmitRequest
{
    public string Description { get; set; } = string.Empty;
}
#endif
