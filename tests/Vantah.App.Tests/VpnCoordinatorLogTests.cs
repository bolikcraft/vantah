using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// Лог координатора: смены состояния с источником, события истории и подавленный первый
/// промах опроса. По этим строкам восстанавливается цепочка «ответ CLI → разбор → что показали».
/// </summary>
public class VpnCoordinatorLogTests
{
    [Fact]
    public async Task Disabled_log_writes_nothing()
    {
        var vpn = new ScriptedVpnService().Enqueue(Connected);
        var (coord, _, log) = Make(vpn, enabled: false);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.DisconnectAsync(TestContext.Current.CancellationToken);

        Assert.Empty(log.Lines);
    }

    [Fact]
    public async Task Poll_logs_a_state_change_once()
    {
        var vpn = new ScriptedVpnService()
            .Enqueue(Connected)
            .Enqueue(VpnStatus.Reconnecting("AMSTERDAM", "TUN", "tun0"))
            .Enqueue(VpnStatus.Reconnecting("AMSTERDAM", "TUN", "tun0"));
        var (coord, _, log) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        var afterChange = States(log);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { "state: Unknown → Connected (опрос)", "state: Connected → Connecting (опрос)" },
            afterChange);
        // Тот же Reconnecting второй раз — состояние не менялось, новых строк нет.
        Assert.Equal(afterChange, States(log));
    }

    [Fact]
    public async Task Connect_and_disconnect_name_their_own_source()
    {
        var vpn = new ScriptedVpnService().Enqueue(Connected);
        var (coord, _, log) = Make(vpn);

        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);
        await coord.DisconnectAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new[]
            {
                "state: Unknown → Connecting (connect)",
                "state: Connecting → Connected (connect)",
                "state: Connected → Disconnecting (disconnect)",
                "state: Disconnecting → Disconnected (disconnect)",
            },
            States(log));
    }

    [Fact]
    public async Task History_session_open_and_close_are_logged()
    {
        var vpn = new ScriptedVpnService().Enqueue(Connected).Enqueue(VpnStatus.Disconnected);
        var (coord, _, log) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { "история: открыта сессия AMSTERDAM", "история: закрыта сессия AMSTERDAM" },
            log.Lines.Where(l => l.StartsWith("история:", StringComparison.Ordinal)).ToArray());
    }

    [Fact]
    public async Task Repeated_polls_of_the_same_session_add_no_history_lines()
    {
        var vpn = new ScriptedVpnService().Enqueue(Connected);
        var (coord, _, log) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Single(log.Lines, l => l.StartsWith("история:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task First_failed_poll_is_logged_as_suppressed_and_the_second_switches_to_error()
    {
        var vpn = new ScriptedVpnService().Enqueue(Connected).Fail("daemon hiccup").Fail("daemon hiccup");
        var (coord, store, log) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal("опрос не удался (1/2), состояние оставлено прежним: daemon hiccup", log.Lines[^1]);
        Assert.Equal(ConnectionState.Connected, store.Current.Connection);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal("опрос не удался (2/2): daemon hiccup", log.Lines[^2]);
        Assert.Equal("state: Connected → Error (опрос)", log.Lines[^1]);
    }

    [Fact]
    public async Task Multiline_error_text_stays_on_one_line()
    {
        var vpn = new ScriptedVpnService().Fail("first line\nsecond line");
        var (coord, _, log) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal("опрос не удался (1/2), состояние оставлено прежним: first line second line", log.Lines[^1]);
    }

    private static VpnStatus Connected => new(true, "AMSTERDAM", "TUN", "tun0");

    private static string[] States(FakeAppLog log) =>
        log.Lines.Where(l => l.StartsWith("state:", StringComparison.Ordinal)).ToArray();

    private static (VpnCoordinator Coordinator, AppStateStore Store, FakeAppLog Log) Make(
        IVpnService vpn, bool enabled = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var log = new FakeAppLog { Enabled = enabled };

        var coord = new VpnCoordinator(vpn, traffic, store, history, ipStore, new FakeAuthService(),
            new LastLocationStore(Path.Combine(dir, "last-location")), null, log);
        return (coord, store, log);
    }
}

/// <summary>Дубль VPN-сервиса по сценарию: очередь статусов и сбоев, последний ответ повторяется.</summary>
file sealed class ScriptedVpnService : IVpnService
{
    private readonly Queue<object> _answers = new();
    private object _last = VpnStatus.Disconnected;

    public ScriptedVpnService Enqueue(VpnStatus status) { _answers.Enqueue(status); return this; }
    public ScriptedVpnService Fail(string message) { _answers.Enqueue(new Exception(message)); return this; }

    public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (_answers.Count > 0) _last = _answers.Dequeue();
        if (_last is Exception ex) return Task.FromException<VpnStatus>(ex);
        return Task.FromResult((VpnStatus)_last);
    }

    public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
        IpVersionPreference ipVersion = IpVersionPreference.Auto,
        bool killSwitch = false, CancellationToken ct = default) => GetStatusAsync(ct);

    public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
        Task.FromResult(VpnStatus.Disconnected);

    public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Location>>([]);

    public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
        Task.FromResult(new License("", "", 0, null));

    public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>("test");
}
