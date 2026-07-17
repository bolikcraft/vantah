using Vantah.Core.Config;
using Vantah.Core.Models;

namespace Vantah.Core.Vpn;

/// <summary>Персист предпочтения версии IP — ~/.config/vantah/ip-version.</summary>
public sealed class IpVersionStore
{
    private readonly string _path;

    public IpVersionStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "ip-version");
    }

    public IpVersionPreference Load()
    {
        try
        {
            if (!File.Exists(_path)) return IpVersionPreference.Auto;
            return Enum.TryParse<IpVersionPreference>(File.ReadAllText(_path).Trim(), out var v)
                ? v : IpVersionPreference.Auto;
        }
        catch { return IpVersionPreference.Auto; }
    }

    public void Save(IpVersionPreference value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, value.ToString());
    }
}
