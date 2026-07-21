using System.Text.Json;
using Vantah.Core.Config;

namespace Vantah.Core.Favorites;

public sealed class FavoritesStore
{
    private readonly string _path;

    public FavoritesStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "favorites.json");
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
        SecureFile.WriteAllText(_path, JsonSerializer.Serialize(keys.Distinct().ToArray()));
    }
}
