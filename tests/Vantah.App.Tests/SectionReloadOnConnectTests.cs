using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Exclusions;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Localization;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Update;
using Vantah.Core.Vpn;
using Location = Vantah.Core.Models.Location;

/// <summary>
/// Разделы, не прочитанные без VPN, обязаны перечитать себя после подключения: без этого
/// экран остаётся с текстом ошибки, пока пользователь не нажмёт «Обновить» руками.
/// </summary>
public class SectionReloadOnConnectTests
{
    /// <summary>Список локаций: первый вызов падает (если так задано), следующие отдают данные.</summary>
    private sealed class FlakyLocationsVpn(bool failFirstCall = true) : IVpnService
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default)
        {
            Calls++;
            if (Calls == 1 && failFirstCall) throw new InvalidOperationException("cli is down");
            // Location(IsoCode, Country, City, PingMs) — порядок именно такой.
            return Task.FromResult<IReadOnlyList<Location>>(
                [new Location("NL", "Netherlands", "Amsterdam", 12)]);
        }

        public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
            IpVersionPreference ipVersion = IpVersionPreference.Auto,
            bool killSwitch = false, CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
            Task.FromResult(new License("u@e", "Premium", 5, null));
        public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>("test");
    }

    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));

    private static LocationsViewModel NewLocations(IVpnService vpn, AppStateStore store)
    {
        var temp = TempDir();
        var coordinator = new VpnCoordinator(
            vpn, new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(temp, "history")),
                new ActiveSessionStore(Path.Combine(temp, "connection-active"))),
            new IpVersionStore(Path.Combine(temp, "ip-version")), new FakeAuthService());
        return new LocationsViewModel(vpn, coordinator, new FavoritesStore(Path.Combine(temp, "favorites")), store);
    }

    [AvaloniaFact]
    public async Task Locations_report_failure_and_reload_on_demand()
    {
        var vpn = new FlakyLocationsVpn();
        var vm = NewLocations(vpn, new AppStateStore());
        await vm.LoadTask;

        IReloadableSection section = vm;
        Assert.Equal("locations", section.Id);
        Assert.True(section.LoadFailed);

        await section.ReloadIfFailedAsync();

        Assert.False(section.LoadFailed);
        Assert.Single(vm.Items);
        Assert.Equal(2, vpn.Calls);
    }

    [AvaloniaFact]
    public async Task Locations_are_not_reloaded_when_the_first_load_succeeded()
    {
        var vpn = new FlakyLocationsVpn(failFirstCall: false);
        var vm = NewLocations(vpn, new AppStateStore());
        await vm.LoadTask;

        var callsAfterLoad = vpn.Calls;
        await ((IReloadableSection)vm).ReloadIfFailedAsync();

        Assert.Equal(callsAfterLoad, vpn.Calls);
    }

    /// <summary>Исключения: первый вызов падает, следующие отдают снапшот.</summary>
    private sealed class FlakyExclusions : IExclusionsService
    {
        public int Calls { get; private set; }

        public Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default)
        {
            Calls++;
            if (Calls == 1) throw new InvalidOperationException("cli is down");
            return Task.FromResult(new ExclusionsSnapshot(SiteExclusionMode.General, ["example.com"]));
        }

        public Task AddAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
            IReadOnlyList<string> currentDomains, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static DomainsViewModel NewDomains(IExclusionsService exclusions, AppStateStore store) =>
        new(exclusions, new ExclusionsStore(Path.Combine(TempDir(), "site-exclusions")), store);

    [AvaloniaFact]
    public async Task Domains_report_failure_and_reload_on_demand()
    {
        var exclusions = new FlakyExclusions();
        var vm = NewDomains(exclusions, new AppStateStore());
        await vm.LoadTask;

        IReloadableSection section = vm;
        Assert.Equal("domains", section.Id);
        Assert.True(section.LoadFailed);

        await section.ReloadIfFailedAsync();

        Assert.False(section.LoadFailed);
        Assert.True(vm.IsLoaded);
        Assert.Single(vm.Items);
    }

    /// <summary>Исключения, у которых можно задержать ответ (Hold/Release) и один раз уронить
    /// первый вызов — тот же приём, что GatedExclusions в DomainsViewSkeletonTests.</summary>
    private sealed class GatedFlakyExclusions : IExclusionsService
    {
        private TaskCompletionSource? _gate;

        public int GetCalls { get; private set; }
        public bool FailNextCall { get; set; }

        public void Hold() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release()
        {
            var gate = _gate;
            _gate = null;
            gate?.TrySetResult();
        }

        public async Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default)
        {
            if (_gate is { } gate) await gate.Task;
            GetCalls++;
            if (FailNextCall) { FailNextCall = false; throw new InvalidOperationException("cli is down"); }
            return new ExclusionsSnapshot(SiteExclusionMode.General, ["example.com"]);
        }

        public Task AddAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
            IReadOnlyList<string> currentDomains, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>SectionReloader (при подключении VPN) и возврат пользователя на вкладку
    /// «Домены» могут дёрнуть ReloadIfFailedAsync почти одновременно. Пока первая перезагрузка
    /// ещё не отвечала, второй вызов не должен запускать своё собственное чтение CLI —
    /// иначе оба пишут в _all/Items/_appState разом.</summary>
    [AvaloniaFact]
    public async Task Domains_second_reload_call_does_not_duplicate_an_in_flight_one()
    {
        var exclusions = new GatedFlakyExclusions { FailNextCall = true };
        var vm = NewDomains(exclusions, new AppStateStore());
        await vm.LoadTask;   // первая загрузка (в конструкторе) упала

        IReloadableSection section = vm;
        Assert.True(section.LoadFailed);
        Assert.Equal(1, exclusions.GetCalls);

        exclusions.Hold();   // следующий GetAsync зависнет, пока не отпустим гейт
        var first = section.ReloadIfFailedAsync();
        var second = section.ReloadIfFailedAsync();   // перезагрузка уже идёт

        exclusions.Release();
        await first;
        await second;

        Assert.Equal(2, exclusions.GetCalls);   // 1 неудачный старт + 1 перезагрузка, не 3
        Assert.False(section.LoadFailed);
        Assert.Same(first, second);   // второй вызов вернул ту же задачу, а не запустил новую
    }

    private static ConfigViewModel NewConfig(FakeConfigService config, AppStateStore store) =>
        new(config, store,
            new LanguageStore(Path.Combine(TempDir(), "language")),
            new FakeUpdateChecker(new UpdateStatus(true, "1.8.0")),
            new FakeLogExporter(), () => Task.FromResult<string?>(null));

    [AvaloniaFact]
    public async Task Settings_report_failure_and_reload_on_demand()
    {
        var config = new FakeConfigService { GetError = new InvalidOperationException("cli is down") };
        var vm = NewConfig(config, new AppStateStore());
        await vm.LoadTask;

        IReloadableSection section = vm;
        Assert.Equal("settings", section.Id);
        Assert.True(section.LoadFailed);

        config.GetError = null;
        await section.ReloadIfFailedAsync();

        Assert.False(section.LoadFailed);
        Assert.True(vm.IsLoaded);
    }

    /// <summary>
    /// Главный регресс-сценарий: автоподключение на старте обычно поднимает туннель за
    /// 2–6 секунд, а стартовое чтение конфига висит до таймаута CLI (~15 с). Переход в
    /// Connected случается ДО того, как первое чтение провалилось — LoadFailed в этот момент
    /// ещё false, и старая реализация SectionReloader тихо пропускала раздел навсегда: следующего
    /// перехода в Connected уже не будет. Раздел обязан быть перечитан ПОСЛЕ того, как первая
    /// загрузка (уже шедшая на момент подключения) закончится провалом.
    /// </summary>
    [AvaloniaFact]
    public async Task Settings_reload_after_connect_retries_a_load_that_was_still_in_flight()
    {
        var config = new FakeConfigService
        {
            GetError = new InvalidOperationException("cli is down"),
            FailOnlyOnce = true,   // первое чтение падает, повтор (после провала) — успешен
        };
        config.HoldGet();          // первое чтение задержано — как реальный CLI-таймаут
        var store = new AppStateStore();
        var vm = NewConfig(config, store);           // LoadTask стартовал в конструкторе и висит
        var reloader = new SectionReloader(store, [vm]);

        // Переход в Connected — ДО того, как первое чтение провалилось.
        store.Set(s => s with { Connection = ConnectionState.Connected });
        Assert.False(vm.LoadTask.IsCompleted);

        config.ReleaseGet();                          // первое чтение отвечает и падает
        await reloader.LastRunTask;
        await vm.LoadTask;   // дождаться итога того, что реально сделала загрузка (1-й или 2-й попытки)

        Assert.False(((IReloadableSection)vm).LoadFailed);
        Assert.True(vm.IsLoaded);
    }

    private static Control? Named(Window window, string name) =>
        window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);

    // Пока идёт повторное чтение, окно «Настройки» показывает скелетон, а не форму с дефолтами
    // вьюмодели: иначе пользователь примет чужие значения за свои настройки.
    [AvaloniaFact]
    public async Task Settings_window_shows_the_skeleton_while_retrying_after_connect()
    {
        var config = new FakeConfigService { GetError = new InvalidOperationException("cli is down") };
        var store = new AppStateStore();
        var vm = NewConfig(config, store);
        await vm.LoadTask;

        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 820, Height = 820 };
        window.Show();
        Assert.False(Named(window, "ConfigSkeleton")!.IsVisible);
        Assert.False(Named(window, "ConfigForm")!.IsVisible);

        config.GetError = null;
        config.HoldGet();                                   // задерживаем ответ CLI
        var reloader = new SectionReloader(store, [vm]);
        store.Set(s => s with { Connection = ConnectionState.Connected });

        Assert.True(Named(window, "ConfigSkeleton")!.IsVisible);   // идёт загрузка — скелетон
        Assert.False(Named(window, "ConfigForm")!.IsVisible);

        config.ReleaseGet();
        await reloader.LastRunTask;

        Assert.False(Named(window, "ConfigSkeleton")!.IsVisible);  // данные пришли — форма
        Assert.True(Named(window, "ConfigForm")!.IsVisible);
    }

    // Тот же приём, что Settings_window_shows_the_skeleton_while_retrying_after_connect,
    // для вкладки «Домены»: пока идёт повторное чтение после подключения, окно обязано
    // показывать скелетон, а не пустой список — иначе пустой DomainsContent на мгновение
    // читается как «исключений нет», хотя список ещё не пришёл.
    [AvaloniaFact]
    public async Task Domains_window_shows_the_skeleton_while_retrying_after_connect()
    {
        var exclusions = new GatedFlakyExclusions { FailNextCall = true };
        var store = new AppStateStore();
        var vm = NewDomains(exclusions, store);
        await vm.LoadTask;   // первая загрузка (в конструкторе) упала

        var window = new Window { Content = new DomainsView { DataContext = vm }, Width = 700, Height = 600 };
        window.Show();
        Assert.False(Named(window, "DomainsSkeleton")!.IsVisible);
        Assert.False(Named(window, "DomainsContent")!.IsVisible);

        exclusions.Hold();                                  // задерживаем ответ CLI
        var reloader = new SectionReloader(store, [vm]);
        store.Set(s => s with { Connection = ConnectionState.Connected });

        Assert.True(Named(window, "DomainsSkeleton")!.IsVisible);   // идёт загрузка — скелетон
        Assert.False(Named(window, "DomainsContent")!.IsVisible);

        exclusions.Release();
        await reloader.LastRunTask;

        Assert.False(Named(window, "DomainsSkeleton")!.IsVisible);  // данные пришли — список
        Assert.True(Named(window, "DomainsContent")!.IsVisible);
    }
}
