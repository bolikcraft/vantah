using Vantah.Core.Config;

namespace Vantah.Core.Vpn;

/// <summary>Персист последней успешной локации — ~/.config/vantah/last-location.</summary>
public sealed class LastLocationStore
{
    private readonly string _path;

    public LastLocationStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "last-location");
    }

    public string? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var text = File.ReadAllText(_path).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch { return null; }
    }

    public void Save(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, location.Trim());
    }
}
