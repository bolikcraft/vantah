using Vantah.Core.Errors;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tray;
using Vantah.Core.Cli;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;

namespace Vantah.App.Tests.Tray;

/// <summary>
/// Зелёная сборка не доказывает, что иконка в трее реально переключается: строчка
/// «_icon.Icon = ...» компилируется и в мёртвом Apply(). Поэтому поднимаем контроллер
/// в headless и смотрим, что лежит в TrayIcon после смены состояния в сторе.
/// </summary>
public class TrayIconControllerTests
{
    [AvaloniaFact]
    public void Icon_follows_connection_state()
    {
        var store = new AppStateStore();
        var icons = new TrayIconSet();
        var controller = NewController(store, icons);
        var trayIcon = controller._icon;

        // Стартовая иконка — из текущего снимка, до всяких событий.
        Assert.Same(icons.For(ConnectionState.Disconnected), trayIcon.Icon);

        foreach (var state in new[]
                 {
                     ConnectionState.Connecting,
                     ConnectionState.Connected,
                     ConnectionState.Disconnecting,
                     ConnectionState.Disconnected,
                     ConnectionState.Error,
                 })
        {
            store.Set(s => s with { Connection = state });
            Dispatcher.UIThread.RunJobs();  // Apply() постится в UI-поток

            Assert.Same(icons.For(state), trayIcon.Icon);
        }
    }

    [AvaloniaFact]
    public void Connected_and_disconnected_icons_are_not_the_same()
    {
        var store = new AppStateStore();
        var icons = new TrayIconSet();
        var trayIcon = NewController(store, icons)._icon;

        store.Set(s => s with { Connection = ConnectionState.Connected });
        Dispatcher.UIThread.RunJobs();
        var connected = trayIcon.Icon;

        store.Set(s => s with { Connection = ConnectionState.Disconnected });
        Dispatcher.UIThread.RunJobs();

        Assert.NotSame(connected, trayIcon.Icon);
    }

    /// <summary>
    /// Состояние Error намеренно рисуется тем же глифом, что и Disconnected (туннеля нет —
    /// трафик не защищён), поэтому причина ошибки обязана быть в подсказке: это единственное
    /// место, где пользователь увидит её, не открывая окно.
    /// </summary>
    [AvaloniaFact]
    public void Tooltip_shows_error_reason_only_in_error_state()
    {
        var store = new AppStateStore();
        var trayIcon = NewController(store, new TrayIconSet())._icon;

        const string reason = "adguardvpn-cli: no route to host";
        var loc = Localizer.Instance;

        store.Set(s => s with { Connection = ConnectionState.Error, Error = AppError.Cli(reason) });
        Dispatcher.UIThread.RunJobs();

        var tip = trayIcon.ToolTipText!;
        Assert.Contains(loc.Format(LocKeys.Tray_Tooltip_ErrorFormat, reason), tip);
        // Строка ошибки не затирает базовые строки подсказки и не затирается ими:
        // «состояние» + «ошибка» + «домены» — ровно три строки.
        Assert.Contains(loc.Format(LocKeys.Tray_Tooltip_DomainsFormat, 0), tip);
        Assert.Equal(3, tip.Split('\n').Length);

        // Обычное «отключено» — без строки ошибки (и без её остатков от прошлого снимка).
        store.Set(s => s with { Connection = ConnectionState.Disconnected, Error = null });
        Dispatcher.UIThread.RunJobs();

        tip = trayIcon.ToolTipText!;
        Assert.DoesNotContain(reason, tip);
        Assert.Equal(2, tip.Split('\n').Length);
    }

    /// <summary>
    /// Обход дубля иконки в GNOME умеет закрывать только соединение настоящей
    /// StatusNotifier-реализации. Там, где его не нашли (headless, XEmbed-трей, другая версия
    /// Avalonia), пересоздание обязано быть полным no-op: сняв иконку без закрытия соединения,
    /// мы оставили бы в панели призрак — то есть сделали бы ровно то, что чиним.
    /// </summary>
    [AvaloniaFact]
    public async Task Rebuild_is_a_no_op_when_the_dbus_connection_is_not_found()
    {
        var store = new AppStateStore();
        var icons = new TrayIconSet();
        var controller = NewController(store, icons);
        var before = controller._icon;

        await controller.RebuildAsync();

        Assert.Same(before, controller._icon);

        // И контроллер продолжает работать: состояние по-прежнему доезжает до иконки.
        store.Set(s => s with { Connection = ConnectionState.Connected });
        Dispatcher.UIThread.RunJobs();
        Assert.Same(icons.For(ConnectionState.Connected), before.Icon);
    }

    /// <summary>
    /// Полное пересоздание: новая иконка, закрытое старое соединение, собранное заново меню и
    /// текущее состояние на месте. Меню здесь — не формальность: NativeMenuItem, однажды
    /// попавший в NativeMenu, во второе меню не добавляется (Avalonia бросает «already has a
    /// parent»), так что переиспользование пунктов уронило бы пересоздание на первом же вызове.
    /// </summary>
    [AvaloniaFact]
    public async Task Rebuild_replaces_the_icon_and_reapplies_state()
    {
        var store = new AppStateStore();
        var icons = new TrayIconSet();
        var controller = NewController(store, icons);

        store.Set(s => s with { Connection = ConnectionState.Connected });
        Dispatcher.UIThread.RunJobs();
        var before = controller._icon;

        var connection = new SpyDisposable();
        await controller.RebuildAsync(_ => connection);
        var after = controller._icon;

        Assert.NotSame(before, after);
        Assert.True(connection.Disposed);

        // Состояние и подписи применены сразу, без ожидания следующего снимка стора.
        Assert.Same(icons.For(ConnectionState.Connected), after.Icon);
        var headers = after.Menu!.Items.OfType<NativeMenuItem>().Select(i => i.Header).ToList();
        Assert.Contains(Localizer.Instance[LocKeys.Tray_Exit], headers);
        Assert.Contains(Localizer.Instance[LocKeys.Tray_Fastest], headers);
        Assert.Contains(Localizer.Instance[LocKeys.Common_Disconnect], headers);

        // Подписка на стор пережила пересоздание и не задвоилась: обновления доезжают до
        // НОВОЙ иконки, а старая больше никем не трогается.
        store.Set(s => s with { Connection = ConnectionState.Disconnected });
        Dispatcher.UIThread.RunJobs();
        Assert.Same(icons.For(ConnectionState.Disconnected), after.Icon);
        Assert.Same(icons.For(ConnectionState.Connected), before.Icon);
    }

    /// <summary>Пересоздание не одноразовое: экран блокируют не единожды за сеанс.</summary>
    [AvaloniaFact]
    public async Task Rebuild_can_run_more_than_once()
    {
        var store = new AppStateStore();
        var icons = new TrayIconSet();
        var controller = NewController(store, icons);

        var first = controller._icon;
        await controller.RebuildAsync(_ => new SpyDisposable());
        var second = controller._icon;
        await controller.RebuildAsync(_ => new SpyDisposable());
        var third = controller._icon;

        Assert.NotSame(first, second);
        Assert.NotSame(second, third);

        store.Set(s => s with { Connection = ConnectionState.Connected });
        Dispatcher.UIThread.RunJobs();
        Assert.Same(icons.For(ConnectionState.Connected), third.Icon);
    }

    private sealed class SpyDisposable : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    // Пути хранилищ уводим в temp: тест не должен читать настоящий ~/.config пользователя.
    private static TrayIconController NewController(AppStateStore store, TrayIconSet icons)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var runner = new CliRunner("adguardvpn-cli");
        var coordinator = new VpnCoordinator(
            new VpnService(runner),
            new TrafficMonitor(new SysfsTrafficReader()),
            store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(temp, "history")),
                new ActiveSessionStore(Path.Combine(temp, "connection-active"))),
            new IpVersionStore(Path.Combine(temp, "ip-version")),
            new Fakes.FakeAuthService());

        return new TrayIconController(
            coordinator,
            store,
            new FavoritesStore(Path.Combine(temp, "favorites.json")),
            new Window(),
            icons);
    }
}
