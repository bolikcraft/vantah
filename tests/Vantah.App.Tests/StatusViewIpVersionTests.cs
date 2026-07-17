using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.History;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

public class StatusViewIpVersionTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "ip-version");

    [Fact]
    public void Selecting_ip_version_persists_it()
    {
        var path = TempPath();
        var vm = MakeStatusVm(new IpVersionStore(path));

        vm.SelectedIpVersion = IpVersionPreference.IPv6Only;

        Assert.Equal(IpVersionPreference.IPv6Only, new IpVersionStore(path).Load());
    }

    [Fact]
    public void Initial_selection_reflects_stored_value()
    {
        var path = TempPath();
        new IpVersionStore(path).Save(IpVersionPreference.IPv4Only);
        var vm = MakeStatusVm(new IpVersionStore(path));

        Assert.Equal(IpVersionPreference.IPv4Only, vm.SelectedIpVersion);
    }

    private static StatusViewModel MakeStatusVm(IpVersionStore ipStore)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var coord = new VpnCoordinator(new FakeVpnService(), traffic, store, history, ipStore);
        var logReader = new VpnLogReader(Path.Combine(dir, "vpn.log"));
        return new StatusViewModel(coord, store, logReader,
            new HistoryViewModel(coord, store), ipStore);
    }
}
