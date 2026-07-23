using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Location = Vantah.Core.Models.Location;

/// <summary>
/// Загрузка вкладки «Лицензия»: LicenseViewModel.RefreshAsync правит Email/Plan/Devices/
/// Renewal/Status/Error ПОСЛЕ await IVpnService.GetLicenseAsync(). В реальном запуске
/// CLI-обёртка возвращает управление на потоке пула, поэтому эту правку маршалим на
/// UI-поток — здесь проверяем наблюдаемый контракт загрузки (данные наполняются без
/// исключения), а не саму потокобезопасность: headless-продолжение всегда возвращается
/// на UI-поток.
/// </summary>
public class LicenseViewModelThreadingTests
{
    // GetLicenseAsync, отвечающий АСИНХРОННО (через Task.Yield) — как ScriptedVpn в LocationsViewTests.
    private sealed class ScriptedVpn(License license) : IVpnService
    {
        public async Task<License> GetLicenseAsync(CancellationToken ct = default)
        {
            await Task.Yield();
            return license;
        }

        public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Location>>([]);
        public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
            IpVersionPreference ipVersion = IpVersionPreference.Auto,
            bool killSwitch = false, CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>("test");
    }

    // Логаут этим тестам не нужен, но конструктор его требует: собираем координатор на фейках.
    private static LicenseViewModel NewVm(IVpnService vpn)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var auth = new FakeAuthService();
        var coordinator = new VpnCoordinator(
            vpn, new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(temp, "history")),
                new ActiveSessionStore(Path.Combine(temp, "connection-active"))),
            new IpVersionStore(Path.Combine(temp, "ip-version")), auth);
        return new LicenseViewModel(vpn, auth, coordinator);
    }

    [AvaloniaFact]
    public async Task Successful_load_populates_the_license_fields()
    {
        var license = new License("user@example.com", "Premium", 5, "2027-01-01");
        var vm = NewVm(new ScriptedVpn(license));

        await vm.LoadTask;

        Assert.Equal("user@example.com", vm.Email);
        Assert.Equal("Premium", vm.Plan);
        Assert.Equal("5", vm.Devices);
        Assert.Equal("2027-01-01", vm.Renewal);
        Assert.Null(vm.Error);
        Assert.False(vm.IsBusy);
    }

    // Заполнение полей, привязанных к отрисованному контролу, при асинхронном ответе CLI
    // не должно падать и обязано наполнить их — это и есть регресс cross-thread правки.
    [AvaloniaFact]
    public async Task Rendered_view_populates_on_async_load()
    {
        var license = new License("user@example.com", "Premium", 5, "2027-01-01");
        var vm = NewVm(new ScriptedVpn(license));
        var window = new Window { Content = new LicenseView { DataContext = vm }, Width = 400, Height = 400 };
        window.Show();

        await vm.LoadTask;

        Assert.Equal("user@example.com", vm.Email);
        Assert.Null(vm.Error);
    }
}
