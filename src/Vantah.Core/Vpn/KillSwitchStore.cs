using Vantah.Core.Config;

namespace Vantah.Core.Vpn;

/// <summary>Персист kill switch (флаг `connect --boot`) — ~/.config/vantah/killswitch.</summary>
public sealed class KillSwitchStore
{
    private readonly string _path;

    public KillSwitchStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "killswitch");
    }

    public bool Load()
    {
        try
        {
            if (!File.Exists(_path)) return false;
            return bool.TryParse(File.ReadAllText(_path).Trim(), out var v) && v;
        }
        catch { return false; }
    }

    public void Save(bool value)
    {
        SecureFile.WriteAllText(_path, value.ToString());
    }
}
