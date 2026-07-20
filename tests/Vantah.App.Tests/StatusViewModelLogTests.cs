using Avalonia.Headless.XUnit;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.History;
using Vantah.Core.Logs;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

public class StatusViewModelLogTests
{
    [AvaloniaFact]
    public async Task Log_is_populated_from_reader_off_ui_thread()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var logPath = Path.Combine(dir, "app.log");
        File.WriteAllText(logPath, "line A\nMEANINGFUL_LINE\nline C\n");
        var reader = new VpnLogReader(logPath);

        var vm = MakeStatusVm(dir, reader);
        await vm.LogRefreshTask;

        Assert.Contains("MEANINGFUL_LINE", vm.Log);
    }

    private static StatusViewModel MakeStatusVm(string dir, VpnLogReader logReader)
    {
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var coord = new VpnCoordinator(new FakeVpnService(), traffic, store, history, ipStore, new FakeAuthService());
        return new StatusViewModel(coord, store, logReader,
            new HistoryViewModel(coord, store), ipStore);
    }
}
