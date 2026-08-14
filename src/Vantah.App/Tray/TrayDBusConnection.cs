using System;
using System.Reflection;
using Avalonia.Controls;

namespace Vantah.App.Tray;

/// <summary>
/// Достаёт D-Bus-соединение, на котором живёт иконка трея. Публичного пути к нему нет:
/// Avalonia прячет реализацию за internal-свойством TrayIcon.Impl, а соединение — за приватным
/// полем DBusTrayIconImpl._connection. Закрыть его нужно потому, что панель GNOME убирает
/// индикатор по исчезновению владельца его bus name: закрытие соединения снимает разом и
/// нормальную запись, и «призрачную», зарегистрированную расширением по уникальному имени.
///
/// Ни одно из этих имён не является контрактом. На другой версии Avalonia, на XEmbed-трее
/// (X11 без StatusNotifier) и на не-Linux вернётся null — вызывающий обязан в этом случае
/// НЕ пересоздавать иконку: без закрытия соединения старая запись останется в панели и иконок
/// станет только больше.
/// </summary>
internal static class TrayDBusConnection
{
    // Avalonia.Controls.TrayIcon: internal ITrayIconImpl? Impl => _impl;
    private const string ImplPropertyName = "Impl";

    // Avalonia.FreeDesktop.DBusTrayIconImpl: private readonly Tmds.DBus.Protocol.DBusConnection? _connection;
    // В рантайме тип не проверяем — достаточно того, что он IDisposable: в разных версиях
    // Avalonia это либо Tmds.DBus.Protocol.DBusConnection, либо обёртка вокруг него. Точный тип
    // текущей версии закреплён тестом AvaloniaTrayInternalsTests: обновится Avalonia — тест
    // упадёт, и обход перепроверят вживую, вместо того чтобы он тихо стал no-op'ом.
    private const string ConnectionFieldName = "_connection";

    public static IDisposable? TryGet(TrayIcon icon)
    {
        try
        {
            var impl = typeof(TrayIcon)
                .GetProperty(ImplPropertyName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(icon);
            if (impl is null) return null;

            return impl.GetType()
                .GetField(ConnectionFieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(impl) as IDisposable;
        }
        catch
        {
            // Рефлексия по чужим приватным членам — ровно то место, где стоит промолчать:
            // не нашли соединение, значит пересоздания не будет, а трей продолжит работать.
            return null;
        }
    }
}
