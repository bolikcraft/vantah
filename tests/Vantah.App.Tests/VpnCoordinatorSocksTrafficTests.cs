using Avalonia.Headless.XUnit;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.Core.Cli;
using Vantah.Core.History;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// В режиме SOCKS туннельного интерфейса нет, и раньше скорость с объёмом всегда показывались
/// нулями. Теперь опрос берёт счётчики по соединениям демона, а порт прокси — из той же
/// строки статуса.
/// </summary>
public class VpnCoordinatorSocksTrafficTests
{
    private sealed class FakeSocksReader : ISocksTrafficReader
    {
        public int? Port;
        public (long rx, long tx)? Read(int socksPort)
        {
            Port = socksPort;
            return (4096, 1024);
        }
    }

    private static VpnCoordinator NewCoordinator(ICliRunner cli, TrafficMonitor traffic, AppStateStore store)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        return new VpnCoordinator(
            new VpnService(cli), traffic, store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
                new ActiveSessionStore(Path.Combine(dir, "connection-active"))),
            new IpVersionStore(Path.Combine(dir, "ip-version")),
            new FakeAuthService());
    }

    [AvaloniaFact]
    public async Task Socks_connection_reports_traffic_from_the_daemon_sockets()
    {
        var socks = new FakeSocksReader();
        var store = new AppStateStore();
        var cli = new FakeCliRunner().Enqueue(new CliResult(
            0, "Connected to SINGAPORE in SOCKS mode, listening on 127.0.0.1:1080", ""));
        var coordinator = NewCoordinator(cli, new TrafficMonitor(new FakeTrafficReader(), socks), store);

        await coordinator.PollOnceAsync();

        Assert.Equal(1080, socks.Port);
        Assert.NotNull(store.Current.Traffic);
        Assert.Equal(4096, store.Current.Traffic!.Value.RxBytes);
        Assert.Equal(1024, store.Current.Traffic!.Value.TxBytes);
    }

    /// <summary>Отключено — считать нечего, счётчики демона не трогаем.</summary>
    [AvaloniaFact]
    public async Task Disconnected_state_does_not_read_the_daemon_sockets()
    {
        var socks = new FakeSocksReader();
        var store = new AppStateStore();
        var cli = new FakeCliRunner().Enqueue(new CliResult(0, "VPN is disconnected", ""));
        var coordinator = NewCoordinator(cli, new TrafficMonitor(new FakeTrafficReader(), socks), store);

        await coordinator.PollOnceAsync();

        Assert.Null(socks.Port);
        Assert.Null(store.Current.Traffic);
    }
}
