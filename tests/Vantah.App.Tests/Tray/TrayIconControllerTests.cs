using System.Reflection;
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
        var icons = new TrayIconSet(TrayIconPolarity.Dark);
        var controller = NewController(store, icons);
        var trayIcon = TrayIconOf(controller);

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
        var icons = new TrayIconSet(TrayIconPolarity.Dark);
        var trayIcon = TrayIconOf(NewController(store, icons));

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
        var trayIcon = TrayIconOf(NewController(store, new TrayIconSet(TrayIconPolarity.Dark)));

        const string reason = "adguardvpn-cli: no route to host";
        var loc = Localizer.Instance;

        store.Set(s => s with { Connection = ConnectionState.Error, Error = reason });
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

    // Пути хранилищ уводим в temp: тест не должен читать настоящий ~/.config пользователя.
    private static TrayIconController NewController(AppStateStore store, TrayIconSet icons)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var runner = new CliRunner("adguardvpn-cli", new PosixProcessKiller("pkill"));
        var coordinator = new VpnCoordinator(
            new VpnService(runner),
            new TrafficMonitor(new SysfsTrafficReader()),
            store,
            new ConnectionHistoryTracker(new ConnectionHistoryStore(Path.Combine(temp, "history"))));

        return new TrayIconController(
            coordinator,
            store,
            new FavoritesStore(Path.Combine(temp, "favorites.json")),
            new Window(),
            icons);
    }

    private static TrayIcon TrayIconOf(TrayIconController controller) =>
        (TrayIcon)typeof(TrayIconController)
            .GetField("_icon", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(controller)!;
}
