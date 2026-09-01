#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Authoritative.Services
{
    /// <summary>
    /// Persistent store for 3D asset files uploaded from the admin panel (glb/gltf/obj/fbx)
    /// and the ZIP-extracted files. Metadata is tracked in a JSON index while binary contents
    /// are written as individual files on disk. Additive to the existing administrative data
    /// stores and reachable only via the authenticated /admin surface.
    /// </summary>
    public interface IAdminFileStore
    {
        string Save(AdminFileMeta meta, byte[] contents);
        bool TryGet(string fileId, out AdminFileMeta? meta, out byte[]? contents);
        IReadOnlyCollection<AdminFileMeta> List();
        bool Delete(string fileId);
    }

    public sealed class AdminFileStore : IAdminFileStore
    {
        readonly ConcurrentDictionary<string, AdminFileMeta> _meta = new(StringComparer.Ordinal);
        readonly object _fileLock = new();
        readonly string _rootDirectory;
        readonly string _indexPath;

        public AdminFileStore()
            : this(Path.Combine(AppContext.BaseDirectory, "data", "admin-files"))
        {
        }

        public AdminFileStore(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
            Directory.CreateDirectory(_rootDirectory);
            _indexPath = Path.Combine(_rootDirectory, "admin-files.json");
            LoadIndex();
        }

        public string Save(AdminFileMeta meta, byte[] contents)
        {
            var id = string.IsNullOrWhiteSpace(meta.Id) ? Guid.NewGuid().ToString("N") : meta.Id.Trim();
            meta.Id = id;
            meta.SavedAtUtc = meta.SavedAtUtc == default ? DateTime.UtcNow : meta.SavedAtUtc;

            var blobPath = BlobPathForId(id);
            lock (_fileLock)
            {
                File.WriteAllBytes(blobPath, contents ?? Array.Empty<byte>());
                _meta[id] = meta;
                PersistIndexLocked();
            }

            return id;
        }

        public bool TryGet(string fileId, out AdminFileMeta? meta, out byte[]? contents)
        {
            meta = null;
            contents = null;

            if (string.IsNullOrWhiteSpace(fileId) || !_meta.TryGetValue(fileId.Trim(), out var stored))
                return false;

            var blobPath = BlobPathForId(stored.Id);
            if (!File.Exists(blobPath))
                return false;

            meta = stored;
            contents = File.ReadAllBytes(blobPath);
            return true;
        }

        public IReadOnlyCollection<AdminFileMeta> List()
        {
            return _meta.Values
                .OrderByDescending(f => f.SavedAtUtc)
                .ToArray();
        }

        public bool Delete(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId) || !_meta.TryRemove(fileId.Trim(), out var removed))
                return false;

            lock (_fileLock)
            {
                var blobPath = BlobPathForId(removed.Id);
                if (File.Exists(blobPath))
                {
                    try { File.Delete(blobPath); } catch { /* best effort */ }
                }
                PersistIndexLocked();
            }

            return true;
        }

        string BlobPathForId(string id) => Path.Combine(_rootDirectory, $"{id}.bin");

        void LoadIndex()
        {
            if (!File.Exists(_indexPath))
                return;

            try
            {
                var raw = File.ReadAllText(_indexPath);
                var entries = Deserialize(raw);
                foreach (var e in entries)
                {
                    _meta[e.Id] = e;
                }
            }
            catch
            {
                // Corrupt/partial index should not prevent startup; start empty.
            }
        }

        void PersistIndexLocked()
        {
            var raw = Serialize(_meta.Values.OrderByDescending(f => f.SavedAtUtc).ToList());
            File.WriteAllText(_indexPath, raw);
        }

        static string Serialize(List<AdminFileMeta> entries)
        {
            var lines = new List<string>();
            foreach (var e in entries)
            {
                var archiveType = string.IsNullOrWhiteSpace(e.ArchiveType) ? string.Empty : e.ArchiveType;
                var parts = new[]
                {
                    Encode(e.Id),
                    Encode(e.Name),
                    e.Size.ToString(),
                    Encode(e.FileType),
                    e.UploadedAtUnixMs.ToString(),
                    Encode(e.RelativePath),
                    e.IsDirectory ? "1" : "0",
                    Encode(archiveType),
                    Encode(e.ExtractionSourceId ?? string.Empty)
                };
                lines.Add(string.Join("|", parts));
            }
            return string.Join("\n", lines);
        }

        static List<AdminFileMeta> Deserialize(string raw)
        {
            var result = new List<AdminFileMeta>();
            if (string.IsNullOrWhiteSpace(raw))
                return result;

            foreach (var line in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 8)
                    continue;

                long size = 0;
                long.TryParse(parts[2], out size);
                long uploaded = 0;
                long.TryParse(parts[4], out uploaded);

                result.Add(new AdminFileMeta
                {
                    Id = Decode(parts[0]),
                    Name = Decode(parts[1]),
                    Size = size,
                    FileType = Decode(parts[3]),
                    UploadedAtUnixMs = uploaded,
                    RelativePath = Decode(parts[5]),
                    IsDirectory = parts[6] == "1",
                    ArchiveType = string.IsNullOrWhiteSpace(Decode(parts[7])) ? null : Decode(parts[7]),
                    ExtractionSourceId = parts.Length > 8 ? Decode(parts[8]) : null,
                    SavedAtUtc = DateTime.UtcNow
                });
            }

            return result;
        }

        static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty));

        static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public sealed class AdminFileMeta
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public long Size { get; set; }
        public string FileType { get; set; } = "GLB";
        public long UploadedAtUnixMs { get; set; }
        public string RelativePath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public string? ArchiveType { get; set; }
        public string? ExtractionSourceId { get; set; }
        public DateTime SavedAtUtc { get; set; }
    }
}
