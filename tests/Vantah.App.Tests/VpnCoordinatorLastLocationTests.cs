using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

public class VpnCoordinatorLastLocationTests
{
    [Fact]
    public async Task Saves_the_manually_picked_location_after_connect()
    {
        var (coord, lastStore) = Make(connectedCity: "AMSTERDAM");
        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);
        Assert.Equal("Amsterdam", lastStore.Load());
    }

    [Fact]
    public async Task Saves_the_status_city_after_a_fastest_connect()
    {
        var (coord, lastStore) = Make(connectedCity: "BERLIN");
        await coord.ConnectAsync(null, fastest: true, TestContext.Current.CancellationToken);
        Assert.Equal("BERLIN", lastStore.Load());
    }

    [Fact]
    public async Task Does_not_save_when_connect_fails_to_connect()
    {
        var (coord, lastStore) = Make(connectedCity: null);
        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);
        Assert.Null(lastStore.Load());
    }

    private static (VpnCoordinator Coordinator, LastLocationStore LastLocation) Make(string? connectedCity)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var lastLocation = new LastLocationStore(Path.Combine(dir, "last-location"));
        var vpn = new FakeConnectingVpnService(connectedCity);

        var coord = new VpnCoordinator(vpn, traffic, store, history, ipStore, new FakeAuthService(), lastLocation);
        return (coord, lastLocation);
    }
}

/// <summary>Дубль VPN-сервиса: connect отдаёт заданный «подключённый» город (или Disconnected, если null).</summary>
file sealed class FakeConnectingVpnService(string? connectedCity) : IVpnService
{
    public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
        IpVersionPreference ipVersion = IpVersionPreference.Auto, CancellationToken ct = default) =>
        Task.FromResult(connectedCity is null
            ? VpnStatus.Disconnected
            : new VpnStatus(true, connectedCity, "tun", "tun0"));

    public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(VpnStatus.Disconnected);
    public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Location>>([]);
    public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
        Task.FromResult(VpnStatus.Disconnected);
    public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
        Task.FromResult(new License("", "", 0, null));
    public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>("test");
}
