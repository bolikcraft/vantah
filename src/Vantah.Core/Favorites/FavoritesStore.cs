using System.Text.Json;

namespace Vantah.Core.Favorites;

public sealed class FavoritesStore
{
    private readonly string _path;

    public FavoritesStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "vantah", "favorites.json");
    }

    public HashSet<string> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            var json = File.ReadAllText(_path);
            var items = JsonSerializer.Deserialize<string[]>(json);
            return items is null ? new() : new HashSet<string>(items);
        }
        catch { return new(); }
    }

    public void Save(IEnumerable<string> keys)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        // Атомарная запись: сначала во временный файл в той же директории, затем move поверх.
        var tmp = Path.Combine(dir, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tmp, JsonSerializer.Serialize(keys.Distinct().ToArray()));
        File.Move(tmp, _path, overwrite: true);
    }
}
