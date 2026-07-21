using Vantah.Core.Config;
using Vantah.Core.Models;

namespace Vantah.Core.Vpn;

/// <summary>Персист режима автоподключения — ~/.config/vantah/autoconnect.</summary>
public sealed class AutoConnectStore
{
    private readonly string _path;

    public AutoConnectStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "autoconnect");
    }

    public AutoConnectMode Load()
    {
        try
        {
            if (!File.Exists(_path)) return AutoConnectMode.Off;
            return Enum.TryParse<AutoConnectMode>(File.ReadAllText(_path).Trim(), out var v)
                ? v : AutoConnectMode.Off;
        }
        catch { return AutoConnectMode.Off; }
    }

    public void Save(AutoConnectMode value)
    {
        SecureFile.WriteAllText(_path, value.ToString());
    }
}
