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
using Vantah.Core.Localization;
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
    /// Без CLI меню не должно предлагать подключиться (это не сработает), а подсказка и
    /// строка статуса — говорить «Отключено» без причины или «Ошибка» с текстом исключения.
    /// </summary>
    [AvaloniaFact]
    public void Missing_cli_is_named_in_tooltip_and_menu_and_connect_items_are_disabled()
    {
        var store = new AppStateStore();
        var trayIcon = NewController(store, new TrayIconSet())._icon;
        var loc = Localizer.Instance;

        store.Set(s => s with
        {
            Connection = ConnectionState.CliMissing,
            Error = new AppError(AppErrorCode.CliMissing, "adguardvpn-cli"),
        });
        Dispatcher.UIThread.RunJobs();

        var tip = trayIcon.ToolTipText!;
        Assert.Contains(loc[LocKeys.Status_CliMissing], tip);
        Assert.DoesNotContain(loc.Format(LocKeys.Tray_Tooltip_ErrorFormat, ""), tip);
        Assert.Equal(3, tip.Split('\n').Length);

        var items = trayIcon.Menu!.Items.OfType<NativeMenuItem>().ToList();
        Assert.Contains(loc[LocKeys.Status_CliMissing], items[0].Header);
        string[] connectItems = [loc[LocKeys.Common_Connect], loc[LocKeys.Tray_Fastest], loc[LocKeys.Tray_Location]];
        Assert.All(items.Where(i => connectItems.Contains(i.Header)), i => Assert.False(i.IsEnabled));
        Assert.Equal(3, items.Count(i => connectItems.Contains(i.Header)));

        // CLI появился — пункты снова доступны.
        store.Set(s => s with { Connection = ConnectionState.Disconnected, Error = null });
        Dispatcher.UIThread.RunJobs();

        Assert.All(items.Where(i => connectItems.Contains(i.Header)), i => Assert.True(i.IsEnabled));
        Assert.DoesNotContain(loc[LocKeys.Status_CliMissing], trayIcon.ToolTipText!);
    }

    /// <summary>
    /// Звёздочка на «Локациях» меняет подменю трея сразу, без перезапуска. Одно сохранение идёт
    /// с потока пула: пункты меню — объекты Avalonia, и без переноса на UI-поток оно бы упало.
    /// </summary>
    [AvaloniaFact]
    public async Task Favorites_submenu_follows_saved_favorites()
    {
        var favorites = new FavoritesStore(Path.Combine(NewTempDir(), "favorites.json"));
        var trayIcon = NewController(new AppStateStore(), new TrayIconSet(), favorites)._icon;

        AssertPlaceholder(FavoriteItems(trayIcon));

        favorites.Save(["EE|Tallinn", "NL|Amsterdam"]);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Amsterdam", "Tallinn"], FavoriteItems(trayIcon).Select(i => i.Header).Order());
        Assert.All(FavoriteItems(trayIcon), i => Assert.True(i.IsEnabled));

        await Task.Run(() => favorites.Save(["NL|Amsterdam"]));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Amsterdam"], FavoriteItems(trayIcon).Select(i => i.Header));

        // Список опустел — снова заглушка, и с подписью, а не пустым пунктом.
        favorites.Save([]);
        Dispatcher.UIThread.RunJobs();
        AssertPlaceholder(FavoriteItems(trayIcon));
    }

    /// <summary>
    /// Смена языка переводит заглушку через поле _noFavorites. Заглушка, собранная заново во
    /// время работы, обязана попасть в это поле, иначе останется на прежнем языке.
    /// </summary>
    [AvaloniaFact]
    public void Placeholder_added_at_runtime_follows_language_switch()
    {
        var favorites = new FavoritesStore(Path.Combine(NewTempDir(), "favorites.json"));
        favorites.Save(["EE|Tallinn"]);
        var trayIcon = NewController(new AppStateStore(), new TrayIconSet(), favorites)._icon;

        favorites.Save([]);
        Dispatcher.UIThread.RunJobs();
        AssertPlaceholder(FavoriteItems(trayIcon));

        try
        {
            Localizer.Instance.SetLanguage("ru");
            Dispatcher.UIThread.RunJobs();

            // FavoriteItems ищет пункт «Локация» по подписи, поэтому и он должен быть на русском.
            var placeholder = Assert.Single(FavoriteItems(trayIcon));
            Assert.Equal("Нет избранного", placeholder.Header);
        }
        finally
        {
            // Localizer.Instance — синглтон на весь тест-хост: не вернём язык — протечёт в соседей.
            Localizer.Instance.SetLanguage(CultureSelector.Default);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static List<NativeMenuItem> FavoriteItems(TrayIcon trayIcon)
    {
        var locations = trayIcon.Menu!.Items.OfType<NativeMenuItem>()
            .Single(i => i.Header == Localizer.Instance[LocKeys.Tray_Location]);
        return locations.Menu!.Items.OfType<NativeMenuItem>().ToList();
    }

    private static void AssertPlaceholder(List<NativeMenuItem> items)
    {
        var placeholder = Assert.Single(items);
        Assert.Equal(Localizer.Instance[LocKeys.Tray_NoFavorites], placeholder.Header);
        Assert.False(placeholder.IsEnabled);
    }

    private static string NewTempDir() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));

    // Пути хранилищ уводим в temp: тест не должен читать настоящий ~/.config пользователя.
    private static TrayIconController NewController(
        AppStateStore store, TrayIconSet icons, FavoritesStore? favorites = null)
    {
        var temp = NewTempDir();
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
            favorites ?? new FavoritesStore(Path.Combine(temp, "favorites.json")),
            new Window(),
            icons);
    }
}
