using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Cli;
using Vantah.Core.Exclusions;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

/// <summary>
/// Полоса вкладок главного окна: рабочих вкладок ровно три, служебные экраны живут в меню «☰»
/// и открываются отдельными окнами. Зелёная сборка этого не доказывает — Avalonia спокойно
/// собирает и разметку с отвязанными пунктами меню, поэтому окно поднимаем headless.
/// </summary>
public class MainWindowTests
{
    // Пути хранилищ уводим в temp: тест не должен читать и портить настоящий ~/.config пользователя.
    private static MainWindowViewModel NewVm()
    {
        var temp = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var runner = new FakeCliRunner();
        var vpn = new VpnService(runner);
        var coordinator = new VpnCoordinator(
            vpn,
            new TrafficMonitor(new FakeTrafficReader()),
            store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(temp, "history")),
                new ActiveSessionStore(Path.Combine(temp, "connection-active"))));

        var exclusionsStore = new ExclusionsStore(Path.Combine(temp, "site-exclusions"));

        return new MainWindowViewModel(
            new StatusViewModel(
                coordinator,
                store,
                new VpnLogReader(Path.Combine(temp, "vpn.log")),
                new HistoryViewModel(coordinator, store)),
            new LocationsViewModel(vpn, coordinator, new FavoritesStore(Path.Combine(temp, "favorites.json")), store),
            new DomainsViewModel(new ExclusionsService(runner, exclusionsStore), exclusionsStore, store),
            new LicenseViewModel(vpn),
            new AboutViewModel(vpn),
            new ProcessesViewModel(new StubMonitor()),
            new ConfigViewModel(new FakeConfigService(), store, new LanguageStore(Path.Combine(temp, "language"))));
    }

    private static MainWindow Show()
    {
        var window = new MainWindow { DataContext = NewVm() };
        window.Show();
        return window;
    }

    /// <summary>
    /// Полоса вкладок самого окна: берём TabControl из корневой сетки, а не первый попавшийся
    /// в дереве — свои TabControl есть и внутри вложенных вью.
    /// </summary>
    private static TabItem[] Tabs(MainWindow window) =>
        ((Grid)window.Content!).Children.OfType<TabControl>().Single().Items.OfType<TabItem>().ToArray();

    /// <summary>Пункты меню «☰» — объекты MenuFlyout, они существуют и до раскрытия флайаута.</summary>
    private static MenuItem[] MenuItems(MainWindow window)
    {
        var button = window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Flyout is MenuFlyout);
        return ((MenuFlyout)button.Flyout!).Items.OfType<MenuItem>().ToArray();
    }

    private static MenuItem Item(MainWindow window, string headerKey) =>
        MenuItems(window).Single(i => (i.Header as string) == Localizer.Instance[headerKey]);

    /// <summary>
    /// Клик по пункту меню — как у пользователя: раскрываем флайаут и жмём мышью по пункту.
    /// Голый <c>RaiseEvent(ClickEvent)</c> на нераскрытом меню не годится: он идёт мимо
    /// DefaultMenuInteractionHandler, флайаут при этом даже не закрывается — то есть проверялась бы
    /// не та последовательность, что происходит на самом деле.
    /// </summary>
    private static void Click(MainWindow window, string headerKey)
    {
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Flyout is MenuFlyout);
        var flyout = (MenuFlyout)button.Flyout!;
        flyout.ShowAt(button);
        Dispatcher.UIThread.RunJobs();
        Assert.True(flyout.IsOpen);

        var item = Item(window, headerKey);
        var center = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window);
        Assert.NotNull(center);   // пункт раскрытого меню обязан быть на экране, а не схлопнут в ноль

        window.MouseMove(center!.Value);
        window.MouseDown(center.Value, MouseButton.Left);
        window.MouseUp(center.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(flyout.IsOpen);   // настоящий клик по пункту меню флайаут закрывает
    }

    private static Window Dialog<TView>(MainWindow window) =>
        window.OwnedWindows.Single(w => w.Content is TView);

    [AvaloniaFact]
    public void The_tab_strip_keeps_only_the_three_working_tabs()
    {
        var window = Show();

        Assert.Equal(3, Tabs(window).Length);
    }

    /// <summary>Меню трея переключает вкладку по индексу 2 — «Домены» обязаны остаться третьими.</summary>
    [AvaloniaFact]
    public void Domains_stay_at_index_two()
    {
        var window = Show();

        var headers = Tabs(window).Select(t => (string)t.Header!).ToArray();
        Assert.Equal(
            [
                Localizer.Instance[LocKeys.Tab_Status],
                Localizer.Instance[LocKeys.Tab_Locations],
                Localizer.Instance[LocKeys.Tab_Domains],
            ],
            headers);
    }

    /// <summary>Выбор языка переезжает в окно настроек (Task 5) — в полосе вкладок его быть не должно.</summary>
    [AvaloniaFact]
    public void The_language_combo_box_is_gone_from_the_tab_strip()
    {
        var window = Show();

        var combos = window.GetVisualDescendants().OfType<ComboBox>()
            .Where(c => c.ItemsSource is IEnumerable<LanguageOption>)
            .ToArray();

        Assert.Empty(combos);
    }

    [AvaloniaFact]
    public void The_menu_lists_the_four_service_screens()
    {
        var window = Show();

        var headers = MenuItems(window).Select(i => (string)i.Header!).ToArray();
        Assert.Equal(
            [
                Localizer.Instance[LocKeys.Menu_Processes],
                Localizer.Instance[LocKeys.Menu_Settings],
                Localizer.Instance[LocKeys.Menu_License],
                Localizer.Instance[LocKeys.Menu_About],
            ],
            headers);
    }

    [AvaloniaFact]
    public void Clicking_a_menu_item_opens_its_screen_in_a_window()
    {
        var window = Show();

        Click(window, LocKeys.Menu_Processes);
        Click(window, LocKeys.Menu_Settings);
        Click(window, LocKeys.Menu_License);
        Click(window, LocKeys.Menu_About);

        Assert.True(Dialog<ProcessesView>(window).IsVisible);
        Assert.True(Dialog<ConfigView>(window).IsVisible);
        Assert.True(Dialog<LicenseView>(window).IsVisible);
        Assert.True(Dialog<AboutView>(window).IsVisible);
    }

    /// <summary>Многоточие в пункте меню обещает диалог; в заголовке самого окна оно лишнее.</summary>
    [AvaloniaFact]
    public void The_window_title_comes_from_the_menu_item_without_the_ellipsis()
    {
        var window = Show();

        Click(window, LocKeys.Menu_Settings);

        var expected = Localizer.Instance[LocKeys.Menu_Settings].TrimEnd('…');
        Assert.Equal(expected, Dialog<ConfigView>(window).Title);
        Assert.DoesNotContain('…', expected);
    }

    /// <summary>Вью и её вьюмодель нужны ровно одни: повторный клик поднимает то же окно.</summary>
    [AvaloniaFact]
    public void Clicking_the_same_item_twice_reuses_one_window()
    {
        var window = Show();
        var vm = (MainWindowViewModel)window.DataContext!;

        Click(window, LocKeys.Menu_Processes);
        var first = Dialog<ProcessesView>(window);
        first.Close();
        Click(window, LocKeys.Menu_Processes);

        Assert.Same(first, Dialog<ProcessesView>(window));
        Assert.Same(vm.Processes, ((ProcessesView)first.Content!).DataContext);
    }
}
