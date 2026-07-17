using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

public class VpnCoordinatorIpVersionTests
{
    [Fact]
    public async Task Connect_uses_the_stored_ip_preference()
    {
        var path = Path.Combine(Path.GetTempPath(), "vantah-tests",
            Guid.NewGuid().ToString("N"), "ip-version");
        var ipStore = new IpVersionStore(path);
        ipStore.Save(IpVersionPreference.IPv4Only);

        var vpn = new FakeVpnService();
        var coord = MakeCoordinator(vpn, ipStore);

        await coord.ConnectAsync("Amsterdam", fastest: false);

        Assert.Equal(IpVersionPreference.IPv4Only, vpn.Connects[0].Ip);
    }

    private static VpnCoordinator MakeCoordinator(FakeVpnService vpn, IpVersionStore ipStore)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        return new VpnCoordinator(vpn, traffic, store, history, ipStore);
    }
}
