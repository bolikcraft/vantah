using System.Text.Json;
using Vantah.Core.Config;

namespace Vantah.Core.History;

/// <summary>Чистое IO: история подключений как JSON Lines под XDG data dir. Кап 12, newest-first.</summary>
public sealed class ConnectionHistoryStore
{
    public const int MaxEntries = 12;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _path;

    public ConnectionHistoryStore(string? path = null) =>
        _path = path ?? Path.Combine(VantahPaths.DataDir, "connections-history");

    public IReadOnlyList<ConnectionHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<ConnectionHistoryEntry>();
            var result = new List<ConnectionHistoryEntry>();
            foreach (var line in File.ReadAllLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (result.Count >= MaxEntries) break;
                try
                {
                    var entry = JsonSerializer.Deserialize<ConnectionHistoryEntry>(line, JsonOptions);
                    if (entry is not null) result.Add(entry);
                }
                catch (JsonException) { /* пропускаем битую строку */ }
            }
            return result;
        }
        catch { return Array.Empty<ConnectionHistoryEntry>(); }
    }

    public void Save(IEnumerable<ConnectionHistoryEntry> entries)
    {
        var capped = entries.Take(MaxEntries).ToArray();
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);

        var lines = capped.Select(e => JsonSerializer.Serialize(e, JsonOptions));
        // Атомарная запись: сначала во временный файл в той же директории, затем move поверх.
        var tmp = Path.Combine(dir, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllLines(tmp, lines);
        File.Move(tmp, _path, overwrite: true);
    }
}
