using System.Reflection;
using System.Threading;
using Avalonia.Controls;

namespace Vantah.App.Tests.Tray;

/// <summary>
/// Обход дубля иконки в GNOME держится на приватных членах Avalonia: TrayDBusConnection достаёт
/// через них D-Bus-соединение иконки, TrayIconGuard — токен петли наблюдения. Оба места написаны
/// так, что при осечке рефлексии молча ничего не делают: это правильно в рантайме, но означает,
/// что смена имён в новой версии Avalonia превратила бы обход в no-op совершенно незаметно —
/// сборка зелёная, тесты зелёные, а иконок в трее снова две и выход снова падает.
///
/// Поэтому контракт закреплён здесь. Тест падает ровно тогда, когда обход перестал быть
/// применимым, и его падение читается как «поднялась версия Avalonia — перепроверить трей
/// вживую», а не как поломка нашего кода.
/// </summary>
public class AvaloniaTrayInternalsTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    private static Type ImplType()
    {
        // DBusTrayIconImpl лежит в Avalonia.FreeDesktop, на который у нас нет прямой ссылки:
        // сборка приезжает транзитивно через Avalonia.Desktop и грузится по имени.
        var type = Assembly.Load("Avalonia.FreeDesktop").GetType("Avalonia.FreeDesktop.DBusTrayIconImpl");
        Assert.NotNull(type);
        return type!;
    }

    /// <summary>Через это свойство оба обхода добираются до реализации иконки.</summary>
    [Fact]
    public void TrayIcon_still_exposes_a_non_public_Impl_property()
    {
        var impl = typeof(TrayIcon).GetProperty("Impl", Hidden);

        Assert.NotNull(impl);
        Assert.True(typeof(Avalonia.Platform.ITrayIconImpl).IsAssignableFrom(impl!.PropertyType));
    }

    /// <summary>
    /// Соединение закрывает TrayDBusConnection: панель GNOME убирает индикатор по исчезновению
    /// владельца его bus name, и «призрачная» запись уходит только вместе с соединением.
    /// Тип проверяем точно: наше собственное соединение (StatusNotifierWatcherMonitor) обязано
    /// быть из той же сборки Tmds.DBus.Protocol, что и соединение трея.
    /// </summary>
    [Fact]
    public void DBusTrayIconImpl_still_holds_the_connection_in_a_private_field()
    {
        var field = ImplType().GetField("_connection", Hidden);

        Assert.NotNull(field);
        Assert.Equal(typeof(Tmds.DBus.Protocol.DBusConnection), field!.FieldType);
    }

    /// <summary>
    /// Токен петли WatchAsync: TrayIconGuard отменяет его до Dispose(), чтобы отмена не улетела
    /// на диспетчер необработанной.
    /// </summary>
    [Fact]
    public void DBusTrayIconImpl_still_holds_the_watch_cts_in_a_private_field()
    {
        var field = ImplType().GetField("_watchCts", Hidden);

        Assert.NotNull(field);
        Assert.Equal(typeof(CancellationTokenSource), field!.FieldType);
    }

    /// <summary>
    /// Приём с отменой до Dispose() работает только потому, что внешний фильтр в WatchAsync
    /// смотрит на _isDisposed, а тот выставляется уже ПОСЛЕ отмены. Само поле — часть того же
    /// контракта: пропало оно — исчезла и логика, из-за которой обход вообще понадобился.
    /// </summary>
    [Fact]
    public void DBusTrayIconImpl_still_has_the_disposed_flag()
    {
        var field = ImplType().GetField("_isDisposed", Hidden);

        Assert.NotNull(field);
        Assert.Equal(typeof(bool), field!.FieldType);
    }
}
