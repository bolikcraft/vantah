using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Vantah.App.Tray;

/// <summary>
/// Разовая проверка: сидит ли на сессионной шине владелец org.kde.StatusNotifierWatcher, то есть
/// примет ли панель нашу иконку.
/// </summary>
internal static class StatusNotifierWatcherProbe
{
    private const string WatcherName = "org.kde.StatusNotifierWatcher";

    /// <summary>Предел ожидания ответа шины.</summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Отвечаем false везде, где спросить не у кого (не Linux, нет шины, шина молчит):
    /// вызывающий тогда показывает окно, а не прячет его в несуществующий трей.
    /// </summary>
    public static async Task<bool> IsPresentAsync()
    {
        if (!OperatingSystem.IsLinux()) return false;
        if (string.IsNullOrEmpty(DBusAddress.Session)) return false;

        try { return await ProbeAsync().WaitAsync(ProbeTimeout); }
        catch { return false; }
    }

    private static async Task<bool> ProbeAsync()
    {
        using var connection = new DBusConnection(new DBusConnectionOptions(DBusAddress.Session!)
        {
            AutoConnect = false,
        });
        await connection.ConnectAsync();
        // Список имён шины, а не NameHasOwner: готовый метод высокого уровня, своё сообщение
        // собирать не нужно.
        return Array.IndexOf(await connection.ListServicesAsync(), WatcherName) >= 0;
    }
}
