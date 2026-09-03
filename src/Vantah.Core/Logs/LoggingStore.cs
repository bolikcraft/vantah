using Vantah.Core.Config;

namespace Vantah.Core.Logs;

/// <summary>Персист настройки «писать лог приложения» — ~/.config/vantah/logging.</summary>
public sealed class LoggingStore
{
    private readonly string _path;

    public LoggingStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "logging");
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
