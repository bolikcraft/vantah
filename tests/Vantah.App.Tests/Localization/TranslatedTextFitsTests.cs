using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Autostart;
using Vantah.Core.Exclusions;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;
using Location = Vantah.Core.Models.Location;

namespace Vantah.App.Tests.Localization;

/// <summary>
/// Подписи не должны обрезаться ни на одном из языков интерфейса.
/// </summary>
/// <remarks>
/// Живой баг, из-за которого тест написан: у кнопки на вкладке «Статус» стояла Width="180" —
/// хватало на «Подключить», но не на «Прервать подключение», и в состоянии «Подключаюсь…»
/// пользователь видел «Прервать подключен…». Такое ловится только измерением: считать символы
/// бесполезно, ширина строки зависит от шрифта, а зелёная сборка про раскладку молчит.
///
/// Поэтому вью поднимаются в headless-окне (Skia, настоящий Inter — см. AvaloniaTestApp),
/// раскладка считается по-настоящему, и сравнивается запрошенная ширина текста с выданной.
/// </remarks>
public class TranslatedTextFitsTests
{
    public static TheoryData<string> Cultures()
    {
        var data = new TheoryData<string>();
        foreach (var code in CultureSelector.Supported) data.Add(code);
        return data;
    }

    /// <summary>Состояния кнопки «Статус» и подпись, которую она обязана показать в каждом.</summary>
    private static readonly (ConnectionState State, string Key)[] ToggleStates =
    [
        (ConnectionState.Disconnected, LocKeys.Common_Connect),
        (ConnectionState.Connected, LocKeys.Common_Disconnect),
        (ConnectionState.Connecting, LocKeys.Common_StopConnecting),
    ];

    /// <summary>
    /// Кнопка подключения на вкладке «Статус» — во всех трёх состояниях и на всех языках.
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(Cultures))]
    public void The_status_toggle_button_fits_its_label_in_every_language(string code)
    {
        WithLanguage(code, () =>
        {
            var failures = new List<string>();
            var (vm, store) = StatusVm();
            var window = Show(new StatusView { DataContext = vm });

            foreach (var (state, key) in ToggleStates)
            {
                store.Set(s => s with { Connection = state, LocationDisplay = "Amsterdam, Netherlands", Mode = "TUN" });
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                var expected = Localizer.Instance[key];
                var button = window.GetVisualDescendants().OfType<Button>()
                    .Single(b => Equals(b.Content, expected) && b.IsEffectivelyVisible);

                failures.AddRange(LayoutOverflow.Find(button).Select(c => $"{code}/{state}: {c}"));
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        });
    }

    /// <summary>
    /// Общая сеть на регрессии: обходим все экраны целиком и ловим любую обрезанную подпись,
    /// а не только кнопку «Статуса».
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(Cultures))]
    public Task Every_screen_fits_its_labels_in_every_language(string code) =>
        WithLanguageAsync(code, async () =>
        {
            var failures = new List<string>();

            // Одно окно на все экраны: каждое showнутое окно тянет за собой композитор, и
            // десятки окон на прогон превращают headless-сессию в неустойчивую.
            var host = new Window { Width = 800, Height = 800 };
            host.Show();

            void Check(string screen, Visual root)
            {
                Dispatcher.UIThread.RunJobs();
                (root as Window ?? host).UpdateLayout();
                failures.AddRange(LayoutOverflow.Find(root).Select(c => $"{code} / {screen}: {c}"));
            }

            void CheckView(string screen, Control view)
            {
                host.Content = view;
                Check(screen, host);
            }

            var (statusVm, statusStore) = StatusVm();
            var statusView = new StatusView { DataContext = statusVm };
            foreach (var (state, _) in ToggleStates)
            {
                statusStore.Set(s => s with
                {
                    Connection = state,
                    LocationDisplay = "Amsterdam, Netherlands",
                    Mode = "TUN",
                    ExclusionsMode = SiteExclusionMode.Selective,
                });
                CheckView($"Статус ({state})", statusView);
            }

            var (locations, locationsStore) = LocationsVm();
            await locations.LoadTask;
            // Одна локация подключена: только у неё кнопка строки показывает «Отключить»
            // (самая длинная подпись колонки) и появляется плашка «✓ Подключено».
            locationsStore.Set(s => s with { Connection = ConnectionState.Connected, Location = "Amsterdam" });
            locations.SortByCommand.Execute("City");   // у заголовка колонки появляется стрелка сортировки
            locations.ToggleFavoritesFirstCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            // Без подключённой строки проверялась бы только короткая подпись «Подключить».
            Assert.Contains(locations.Items, i => i.IsConnected);
            CheckView("Локации", new LocationsView { DataContext = locations });

            var domains = DomainsVm();
            await domains.LoadTask;
            Assert.True(domains.IsLoaded);   // иначе проверяли бы скелетон, а не вкладку
            CheckView("Домены", new DomainsView { DataContext = domains });

            var config = ConfigVm();
            await config.LoadTask;
            Dispatcher.UIThread.RunJobs();
            Assert.True(config.IsLoaded);
            CheckView("Настройки", new ConfigView { DataContext = config });

            var license = LicenseVm();
            await license.LoadTask;
            CheckView("Лицензия", new LicenseView { DataContext = license });

            CheckView("Процессы", new ProcessesView { DataContext = new ProcessesViewModel(new StubMonitor(Processes())) });
            CheckView("О программе", new AboutView { DataContext = new AboutViewModel(new FakeVpnService()) });

            var login = LoginVm();
            var loginView = new LoginView { DataContext = login };
            CheckView("Вход", loginView);
            await login.StartCommand.ExecuteAsync(null);
            CheckView("Вход (ждём браузер)", loginView);

            // Главное окно — полоса вкладок и рабочая область: вкладки проходим переключением,
            // невыбранная вкладка не построена и в дереве её нет.
            var main = MainWindowFactory.Build();
            main.Show();
            for (var tab = 0; tab < 3; tab++)
            {
                ((MainWindowViewModel)main.DataContext!).SelectedTab = tab;
                Check($"Главное окно (вкладка {tab})", main);
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        });

    private static void WithLanguage(string code, Action body) =>
        WithLanguageAsync(code, () => { body(); return Task.CompletedTask; }).GetAwaiter().GetResult();

    private static async Task WithLanguageAsync(string code, Func<Task> body)
    {
        var prev = Localizer.Instance.Language;
        Localizer.Instance.SetLanguage(code);
        Dispatcher.UIThread.RunJobs();
        try { await body(); }
        finally
        {
            // Localizer.Instance — синглтон на весь тест-хост: не вернём язык — протечёт в соседей.
            Localizer.Instance.SetLanguage(prev);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Window Show(Control view)
    {
        var window = new Window { Content = view, Width = 800, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));

    private static (StatusViewModel Vm, AppStateStore Store) StatusVm()
    {
        var dir = TempDir();
        var store = new AppStateStore();
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var coord = Coordinator(new FakeVpnService(), store, ipStore);
        var vm = new StatusViewModel(coord, store, new VpnLogReader(Path.Combine(dir, "vpn.log")),
            new HistoryViewModel(coord, store), ipStore);
        return (vm, store);
    }

    private static VpnCoordinator Coordinator(IVpnService vpn, AppStateStore store, IpVersionStore ipStore)
    {
        var dir = TempDir();
        return new VpnCoordinator(
            vpn, new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
                new ActiveSessionStore(Path.Combine(dir, "connection-active"))),
            ipStore, new FakeAuthService());
    }

    /// <summary>Список локаций с самыми длинными реальными названиями городов и стран.</summary>
    private sealed class LocationsVpn : FakeVpnServiceBase
    {
        public override Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Location>>(
            [
                new("NL", "Netherlands", "Amsterdam", 12),
                new("US", "United States", "Salt Lake City", 148),
                new("AE", "United Arab Emirates", "Dubai", 220),
            ]);
    }

    private static (LocationsViewModel Vm, AppStateStore Store) LocationsVm()
    {
        var dir = TempDir();
        var store = new AppStateStore();
        var vpn = new LocationsVpn();
        var coord = Coordinator(vpn, store, new IpVersionStore(Path.Combine(dir, "ip-version")));
        return (new LocationsViewModel(vpn, coord, new FavoritesStore(Path.Combine(dir, "favorites")), store), store);
    }

    private sealed class LoadedExclusions : IExclusionsService
    {
        public Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new ExclusionsSnapshot(SiteExclusionMode.General,
                ["example.com", "very-long-domain-name.example.org"]));

        public Task AddAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
            IReadOnlyList<string> currentDomains, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static DomainsViewModel DomainsVm() =>
        new(new LoadedExclusions(), new ExclusionsStore(Path.Combine(TempDir(), "site-exclusions")),
            new AppStateStore());

    private static ConfigViewModel ConfigVm()
    {
        var root = TempDir();
        return new ConfigViewModel(
            new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            new AutoConnectStore(Path.Combine(root, "autoconnect")),
            new AutostartService(Path.Combine(root, "autostart"), "vantah", "vantah"),
            killSwitch: new KillSwitchStore(Path.Combine(root, "killswitch")));
    }

    private sealed class LicensedVpn : FakeVpnServiceBase
    {
        public override Task<License> GetLicenseAsync(CancellationToken ct = default) =>
            Task.FromResult(new License("very.long.address@example-mail.org", "AdGuard VPN Unlimited", 10, "2027-01-31"));
    }

    private static LicenseViewModel LicenseVm()
    {
        var vpn = new LicensedVpn();
        var auth = new FakeAuthService();
        var store = new AppStateStore();
        return new LicenseViewModel(vpn, auth,
            Coordinator(vpn, store, new IpVersionStore(Path.Combine(TempDir(), "ip-version"))));
    }

    private static LoginViewModel LoginVm()
    {
        var store = new AppStateStore();
        var auth = new FakeAuthService { State = LoginState.LoggedOut };
        var vm = new LoginViewModel(auth,
            Coordinator(new FakeVpnService(), store, new IpVersionStore(Path.Combine(TempDir(), "ip-version"))));
        vm.BrowserOpener = _ => Task.CompletedTask;
        return vm;
    }

    private static Vantah.Core.Cli.RunningProcess[] Processes()
    {
        const string exe = "/opt/adguardvpn_cli/adguardvpn-cli";
        return
        [
            new(1086935, 1086935, "sudo", ["-b", "env", exe, "connect"], DateTimeOffset.Now.AddMinutes(-6)),
            new(1086937, 1086937, exe, ["connect", "--no-fork", "-l", "Amsterdam"], DateTimeOffset.Now.AddMinutes(-6)),
        ];
    }

    /// <summary>Заглушка IVpnService, у которой тесты переопределяют только нужный метод.</summary>
    private abstract class FakeVpnServiceBase : IVpnService
    {
        public virtual Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public virtual Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Location>>([]);
        public virtual Task<VpnStatus> ConnectAsync(string? location, bool fastest,
            IpVersionPreference ipVersion = IpVersionPreference.Auto,
            bool killSwitch = false, CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public virtual Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public virtual Task<License> GetLicenseAsync(CancellationToken ct = default) =>
            Task.FromResult(new License("", "", 0, null));
        public virtual Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>("1.2.3");
    }
}
