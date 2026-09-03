using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Регресс: при включённом kill switch после обрыва связи `status` отдаёт «Reconnecting to …»
/// либо «Connection lost. Waiting to reconnect to …». Раньше обе формы падали в общую ветку и
/// писались как «отключено»: на «Статусе» появлялась кнопка «Подключить», подпись локации
/// пропадала, а каждый ретрай демона закрывал активную сессию истории.
/// </summary>
public class VpnCoordinatorReconnectingStateTests
{
    [Fact]
    public async Task Poll_that_sees_reconnecting_reports_connecting_and_keeps_location()
    {
        var vpn = new FakeReconnectingVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, store, history, _) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionState.Connected, store.Current.Connection);
        Assert.NotNull(history.Active);

        vpn.Status = VpnStatus.Reconnecting("AMSTERDAM", "TUN", "tun0");
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Connecting, store.Current.Connection);
        Assert.Equal("AMSTERDAM", store.Current.Location);
        Assert.Equal("AMSTERDAM", store.Current.LocationDisplay);
        Assert.Equal("TUN", store.Current.Mode);
        Assert.Equal("tun0", store.Current.Interface);
        Assert.Null(store.Current.Traffic);
        Assert.Null(store.Current.Error);
    }

    [Fact]
    public async Task Poll_that_sees_reconnecting_does_not_close_the_history_session()
    {
        var vpn = new FakeReconnectingVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, _, history, _) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(history.Active);

        // Ретраи kill switch идут подряд: ни один не должен рвать сессию.
        vpn.Status = VpnStatus.Reconnecting("AMSTERDAM", "TUN", "tun0");
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(history.Active);
        Assert.Equal("AMSTERDAM", history.Active!.City);
        Assert.Empty(history.Previous);
    }

    [Fact]
    public async Task Poll_in_reconnecting_fills_location_even_without_a_previous_poll()
    {
        var vpn = new FakeReconnectingVpnService(VpnStatus.Reconnecting("OSLO", "SOCKS", null, "127.0.0.1:1080"));
        var (coord, store, _, _) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Connecting, store.Current.Connection);
        Assert.Equal("OSLO", store.Current.Location);
        Assert.Equal("SOCKS", store.Current.Mode);
        Assert.Null(store.Current.Traffic);
    }

    [Fact]
    public async Task Connect_that_returns_reconnecting_stays_in_connecting()
    {
        var vpn = new FakeReconnectingVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, store, history, _) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(history.Active);

        vpn.Status = VpnStatus.Reconnecting("AMSTERDAM", "TUN", "tun0");
        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Connecting, store.Current.Connection);
        Assert.Null(store.Current.Error);
        Assert.NotNull(history.Active);
        Assert.Equal("AMSTERDAM", history.Active!.City);
    }

    private static (VpnCoordinator Coordinator, AppStateStore Store,
        ConnectionHistoryTracker History, LastLocationStore LastLocation) Make(IVpnService vpn)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var lastLocation = new LastLocationStore(Path.Combine(dir, "last-location"));

        var coord = new VpnCoordinator(vpn, traffic, store, history, ipStore,
            new FakeAuthService(), lastLocation);
        return (coord, store, history, lastLocation);
    }
}

/// <summary>Дубль VPN-сервиса: и connect, и status отдают один и тот же настраиваемый статус.</summary>
file sealed class FakeReconnectingVpnService(VpnStatus status) : IVpnService
{
    public VpnStatus Status { get; set; } = status;

    public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
        IpVersionPreference ipVersion = IpVersionPreference.Auto,
        bool killSwitch = false, CancellationToken ct = default) => Task.FromResult(Status);

    public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(Status);
    public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Location>>([]);
    public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
        Task.FromResult(VpnStatus.Disconnected);
    public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
        Task.FromResult(new License("", "", 0, null));
    public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>("test");
}
