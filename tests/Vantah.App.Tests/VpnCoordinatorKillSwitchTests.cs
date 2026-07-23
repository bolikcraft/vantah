using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.Core.History;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

public class VpnCoordinatorKillSwitchTests
{
    [Fact]
    public async Task Passes_kill_switch_from_store_into_connect()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var vpn = new FakeVpnService();
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var killStore = new KillSwitchStore(Path.Combine(dir, "killswitch"));
        killStore.Save(true);

        var coord = new VpnCoordinator(vpn, traffic, store, history, ipStore,
            new FakeAuthService(), killSwitch: killStore);

        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);

        Assert.True(vpn.Connects[0].KillSwitch);
    }
}
