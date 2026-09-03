using Avalonia.Headless.XUnit;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.Core.Cli;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Регресс: транзиентный сбой `status` (ненулевой код возврата CLI) раньше молча парсился как
/// «отключено» и рвал активную сессию истории пополам через <see cref="ConnectionHistoryTracker"/>.
/// Теперь <see cref="VpnService.GetStatusAsync"/> бросает на !r.Ok, а PollOnceAsync ловит это
/// в общий catch, оставляя историю нетронутой.
/// </summary>
public class VpnCoordinatorHistoryTests
{
    [AvaloniaFact]
    public async Task Transient_status_failure_does_not_finalize_active_history_session()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var cli = new FakeCliRunner()
            .Enqueue(new CliResult(0, "Connected to AMSTERDAM in TUN mode, running on tun0", ""))
            .Enqueue(new CliResult(1, "", "daemon hiccup"))
            .Enqueue(new CliResult(1, "", "daemon hiccup"));
        var vpn = new VpnService(cli);

        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var coord = new VpnCoordinator(vpn, traffic, store, history, ipStore, new FakeAuthService());

        // Первый опрос: успешное подключение — активная сессия истории открывается.
        await coord.PollOnceAsync();
        Assert.NotNull(history.Active);
        Assert.Equal("AMSTERDAM", history.Active!.City);

        // Дальше `status` транзиентно падает (ненулевой код возврата CLI). Раньше это молча
        // парсилось бы как «отключено» и рвало бы активную сессию. В Error уходим со второго
        // сбоя подряд — см. VpnCoordinatorPollFailureTests.
        await coord.PollOnceAsync();
        await coord.PollOnceAsync();

        Assert.NotNull(history.Active);
        Assert.Equal("AMSTERDAM", history.Active!.City);
        Assert.Equal(ConnectionState.Error, store.Current.Connection);
    }
}
