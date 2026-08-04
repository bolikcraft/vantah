using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Vantah.App.Localization;
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
/// «Выйти» на экране аккаунта необратим, поэтому кнопка обязана быть доступна только когда
/// лицензия реально загружена: пока идёт запрос, при ошибке CLI и при неразобранном ответе
/// (не залогинен) нажимать нечего — карточка показывает заглушки «—».
/// </summary>
public class LicenseViewLogoutButtonTests
{
    // Отдаёт лицензию не раньше, чем тест разрешит — так ловится состояние «данные ещё грузятся».
    private sealed class GatedVpn(Task<License> gate) : IVpnService
    {
        public Task<License> GetLicenseAsync(CancellationToken ct = default) => gate;

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

    private static LicenseViewModel NewVm(IVpnService vpn)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var auth = new FakeAuthService();
        var coordinator = new VpnCoordinator(
            vpn, new TrafficMonitor(new FakeTrafficReader()), new AppStateStore(),
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(temp, "history")),
                new ActiveSessionStore(Path.Combine(temp, "connection-active"))),
            new IpVersionStore(Path.Combine(temp, "ip-version")), auth);
        return new LicenseViewModel(vpn, auth, coordinator);
    }

    private static Button LogoutButton(Window window) =>
        window.GetVisualDescendants().OfType<Button>()
            .First(b => (b.Content as string) == Localizer.Instance[LocKeys.Login_Logout]);

    [AvaloniaFact]
    public async Task Logout_is_disabled_while_the_license_is_loading_and_enabled_after()
    {
        var gate = new TaskCompletionSource<License>();
        var vm = NewVm(new GatedVpn(gate.Task));
        var window = new Window { Content = new LicenseView { DataContext = vm }, Width = 500, Height = 400 };
        window.Show();

        Assert.False(LogoutButton(window).IsEnabled);

        gate.SetResult(new License("user@example.com", "Premium", 5, "2027-01-01"));
        await vm.LoadTask;

        Assert.True(LogoutButton(window).IsEnabled);
    }

    [AvaloniaFact]
    public async Task Logout_stays_disabled_when_the_license_did_not_parse()
    {
        // Заглушка LicenseParser при неразобранном выводе: карточка пустая, выходить не из чего.
        var vm = NewVm(new GatedVpn(Task.FromResult(new License("", "UNKNOWN", 0, null))));
        var window = new Window { Content = new LicenseView { DataContext = vm }, Width = 500, Height = 400 };
        window.Show();

        await vm.LoadTask;

        Assert.False(LogoutButton(window).IsEnabled);
    }

    [AvaloniaFact]
    public async Task Logout_stays_disabled_when_the_cli_failed()
    {
        var vm = NewVm(new GatedVpn(Task.FromException<License>(new InvalidOperationException("cli is down"))));
        var window = new Window { Content = new LicenseView { DataContext = vm }, Width = 500, Height = 400 };
        window.Show();

        await vm.LoadTask;

        Assert.NotNull(vm.Error);
        Assert.False(LogoutButton(window).IsEnabled);
    }
}
