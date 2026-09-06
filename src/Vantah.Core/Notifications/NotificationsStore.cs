using Vantah.Core.Config;

namespace Vantah.Core.Notifications;

/// <summary>
/// Персист настройки «уведомлять об обрыве связи» — ~/.config/vantah/notifications.
/// По умолчанию ВКЛЮЧЕНО: без уведомления об обрыве пользователь узнаёт о нём, только
/// если сам посмотрит на значок в трее.
/// </summary>
public sealed class NotificationsStore
{
    private const bool Default = true;

    private readonly string _path;

    public NotificationsStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "notifications");
    }

    public bool Load()
    {
        try
        {
            if (!File.Exists(_path)) return Default;
            return bool.TryParse(File.ReadAllText(_path).Trim(), out var v) ? v : Default;
        }
        catch { return Default; }
    }

    public void Save(bool value)
    {
        SecureFile.WriteAllText(_path, value.ToString());
    }
}
