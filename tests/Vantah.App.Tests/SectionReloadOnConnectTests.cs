using Avalonia.Headless.XUnit;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
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
}
