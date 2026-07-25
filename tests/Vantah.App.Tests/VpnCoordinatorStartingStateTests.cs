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
/// Регресс: при включённом kill switch connect уходит с флагом --boot и возвращается сразу,
/// пока туннель ещё поднимается. Раньше «VPN is starting» парсилось как «отключено», и на
/// вкладке «Статус» на секунды мигало «Отключено», сессия истории закрывалась, а последняя
/// локация не сохранялась.
/// </summary>
public class VpnCoordinatorStartingStateTests
{
    [Fact]
    public async Task Connect_that_returns_starting_stays_in_connecting()
    {
        var (coord, store, _, _) = Make(new FakeStartingVpnService(VpnStatus.Starting));

        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Connecting, store.Current.Connection);
        Assert.Null(store.Current.Error);
    }

    [Fact]
    public async Task Connect_that_returns_starting_does_not_close_the_history_session()
    {
        var vpn = new FakeStartingVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, _, history, _) = Make(vpn);

        // Опрос открывает активную сессию, затем connect (ретрай kill switch) отвечает «starting».
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(history.Active);

        vpn.Status = VpnStatus.Starting;
        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);

        Assert.NotNull(history.Active);
        Assert.Equal("AMSTERDAM", history.Active!.City);
    }

    [Fact]
    public async Task Connect_that_returns_starting_still_saves_the_last_location()
    {
        var (coord, _, _, lastLocation) = Make(new FakeStartingVpnService(VpnStatus.Starting));

        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);

        Assert.Equal("Amsterdam", lastLocation.Load());
    }

    [Fact]
    public async Task Poll_that_sees_starting_reports_connecting_and_keeps_known_location()
    {
        var vpn = new FakeStartingVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, store, _, _) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionState.Connected, store.Current.Connection);

        // Kill switch переподнимает туннель: `status` временно отдаёт «starting».
        vpn.Status = VpnStatus.Starting;
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Connecting, store.Current.Connection);
        Assert.Equal("AMSTERDAM", store.Current.Location);
        Assert.Equal("tun0", store.Current.Interface);
        Assert.Null(store.Current.Traffic);
        Assert.Null(store.Current.Error);
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
file sealed class FakeStartingVpnService(VpnStatus status) : IVpnService
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
