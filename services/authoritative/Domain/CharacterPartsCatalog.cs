#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Authoritative.Domain
{
    public sealed class CharacterPartManifest
    {
        public string Id { get; set; } = string.Empty;
        public List<string> Files { get; set; } = new();
        public string? Gender { get; set; }
        public int? Variant { get; set; }
        public int? Priority { get; set; }
        public string? AttachBone { get; set; }
        public Dictionary<string, object> Meta { get; set; } = new();
    }

    public static class CharacterPartsCatalog
    {
        private static readonly Dictionary<string, CharacterPartManifest> _parts = new(StringComparer.Ordinal);

        public static IReadOnlyDictionary<string, CharacterPartManifest> Parts => _parts;

        static CharacterPartsCatalog()
        {
            try
            {
                var path = Path.Combine("Assets", "Characters", "character_parts_expanded.json");
                if (!File.Exists(path)) return;
                var txt = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(txt);
                if (!doc.RootElement.TryGetProperty("parts", out var partsElem)) return;
                foreach (var p in partsElem.EnumerateArray())
                {
                    var id = p.GetProperty("id").GetString() ?? string.Empty;
                    var files = new List<string>();
                    if (p.TryGetProperty("files", out var farr))
                    {
                        foreach (var f in farr.EnumerateArray())
                            files.Add(f.GetString() ?? string.Empty);
                    }
                    var manifest = new CharacterPartManifest
                    {
                        Id = id,
                        Files = files,
                        Gender = p.TryGetProperty("gender", out var g) ? g.GetString() : null,
                        Variant = p.TryGetProperty("variant", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null,
                        Priority = p.TryGetProperty("priority", out var pr) && pr.ValueKind == JsonValueKind.Number ? pr.GetInt32() : null,
                        AttachBone = p.TryGetProperty("attachBone", out var ab) ? ab.GetString() : null
                    };
                    if (!_parts.ContainsKey(id)) _parts[id] = manifest;
                }
            }
            catch
            {
                // best-effort load; leave empty on error
            }
        }
    }
}
#endif
