using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Auth;
using Vantah.Core.Exclusions;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

/// <summary>
/// Гейт по логину: когда сессии нет — видна форма входа, рабочий UI скрыт; когда есть — наоборот.
/// Зелёная сборка этого не докажет (память проекта: no-op кнопки Kill прошли сборку), поэтому
/// поднимаем окно headless и смотрим фактическую видимость и привязку команды выхода.
/// </summary>
public class MainWindowLoginGateTests
{
    private static (MainWindow Window, AppStateStore Store, FakeAuthService Auth) ShowWith(LoginState state)
    {
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vantah-tests", System.Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        store.Set(s => s with { LoginState = state });

        var runner = new FakeCliRunner();
        var vpn = new VpnService(runner);
        var ipVersionStore = new IpVersionStore(System.IO.Path.Combine(temp, "ip-version"));
        var auth = new FakeAuthService { State = state };
        var coordinator = new VpnCoordinator(
            vpn, new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(System.IO.Path.Combine(temp, "history")),
                new ActiveSessionStore(System.IO.Path.Combine(temp, "connection-active"))),
            ipVersionStore, auth);
        var exclusionsStore = new ExclusionsStore(System.IO.Path.Combine(temp, "site-exclusions"));

        var vm = new MainWindowViewModel(
            new StatusViewModel(coordinator, store, new VpnLogReader(System.IO.Path.Combine(temp, "vpn.log")),
                new HistoryViewModel(coordinator, store), ipVersionStore),
            new LocationsViewModel(vpn, coordinator, new FavoritesStore(System.IO.Path.Combine(temp, "favorites.json")), store),
            new DomainsViewModel(new ExclusionsService(runner, exclusionsStore), exclusionsStore, store),
            new LicenseViewModel(vpn), new AboutViewModel(vpn), new ProcessesViewModel(new StubMonitor()),
            new ConfigViewModel(new FakeConfigService(), store, new Vantah.Core.Localization.LanguageStore(System.IO.Path.Combine(temp, "language")),
                new FakeUpdateChecker(), new FakeLogExporter(), () => System.Threading.Tasks.Task.FromResult<string?>(null)),
            new LoginViewModel(auth, coordinator), auth, coordinator, store);

        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, store, auth);
    }

    private static Border LoginWrapper(MainWindow window) =>
        ((Grid)window.Content!).Children.OfType<Border>().Single();

    private static Grid WorkingArea(MainWindow window) =>
        ((Grid)window.Content!).Children.OfType<Grid>().Single();

    [AvaloniaFact]
    public void Logged_out_shows_login_form_and_hides_working_ui()
    {
        var (window, _, _) = ShowWith(LoginState.LoggedOut);
        Assert.True(LoginWrapper(window).IsVisible);    // форма входа
        Assert.False(WorkingArea(window).IsVisible);    // вкладки/меню скрыты
    }

    [AvaloniaFact]
    public void Logged_in_shows_working_ui_and_hides_login_form()
    {
        var (window, _, _) = ShowWith(LoginState.LoggedIn);
        Assert.False(LoginWrapper(window).IsVisible);
        Assert.True(WorkingArea(window).IsVisible);
    }

    [AvaloniaFact]
    public void Unknown_state_keeps_working_ui_visible()
    {
        // При Unknown (зонд не прошёл) не мигаем формой входа зря — гейт по «!= LoggedOut».
        var (window, _, _) = ShowWith(LoginState.Unknown);
        Assert.True(WorkingArea(window).IsVisible);
        Assert.False(LoginWrapper(window).IsVisible);
    }

    [AvaloniaFact]
    public void Logout_command_calls_auth_and_flips_gate_to_login_form()
    {
        // Пункт «Выйти» дёргает LogoutCommand вьюмодели (Click-обработчик привязан на этапе
        // компиляции XAML). Проверяем, что команда реально вызывает logout и — после того как
        // зонд увидит выход — гейт показывает форму входа.
        var (window, store, auth) = ShowWith(LoginState.LoggedIn);
        var vm = (MainWindowViewModel)window.DataContext!;

        auth.State = LoginState.LoggedOut;            // после logout зонд увидит «не залогинен»
        vm.LogoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, auth.LogoutCalls);
        Assert.Equal(LoginState.LoggedOut, store.Current.LoginState);
        Assert.False(vm.IsLoggedIn);
        Assert.True(LoginWrapper(window).IsVisible);
        Assert.False(WorkingArea(window).IsVisible);
    }
}
