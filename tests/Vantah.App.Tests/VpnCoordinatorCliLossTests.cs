using Vantah.App.Notifications;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.Tests.Notifications;
using Vantah.Core.Cli;
using Vantah.Core.Errors;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.Notifications;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// CLI пропал при подключённом VPN. Раньше менялись только состояние и ошибка: «Статус» держал
/// старые локацию, режим и скорость. Сессию в истории при этом закрывать нельзя: демон без файла
/// CLI может жить дальше. Она ждёт ответа вернувшегося CLI, как сессия, поднятая с диска.
/// </summary>
public class VpnCoordinatorCliLossTests
{
    private static readonly VpnStatus Amsterdam = new(true, "AMSTERDAM", "TUN", "tun0");
    private static readonly VpnStatus Paris = new(true, "PARIS", "TUN", "tun0");

    [Fact]
    public async Task Cli_loss_clears_location_mode_and_traffic()
    {
        var t = Make();
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(t.Store.Current.Traffic);

        t.Vpn.Missing = true;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        var s = t.Store.Current;
        Assert.Equal(ConnectionState.CliMissing, s.Connection);
        Assert.Equal(AppErrorCode.CliMissing, s.Error?.Code);
        Assert.Null(s.Location);
        Assert.Null(s.LocationDisplay);
        Assert.Null(s.Mode);
        Assert.Null(s.Interface);
        Assert.Null(s.Traffic);
    }

    /// <summary>Без сброса базы первый опрос после возврата CLI показал бы скорость за всё время пропажи.</summary>
    [Fact]
    public async Task Cli_loss_resets_the_traffic_base()
    {
        var t = Make();
        t.Traffic.Counters = (1000, 100);
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        t.Vpn.Missing = true;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        // Пауза даёт ненулевой интервал: при нулевом скорость была бы нулём и без сброса.
        await Task.Delay(20, TestContext.Current.CancellationToken);
        t.Traffic.Counters = (900_000, 90_000);
        t.Vpn.Missing = false;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        var sample = Assert.NotNull(t.Store.Current.Traffic);
        Assert.Equal(0, sample.RxBytesPerSec);
        Assert.Equal(0, sample.TxBytesPerSec);
    }

    [Fact]
    public async Task Cli_loss_keeps_the_session_open()
    {
        var t = Make();
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
        var session = t.History.Active;

        t.Vpn.Missing = true;
        for (var i = 0; i < 3; i++)
            await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(session);
        Assert.Equal(session, t.History.Active);
        Assert.Empty(t.History.Previous);
    }

    /// <summary>Иначе сессия закрылась бы моментом возврата CLI и вобрала бы всё время его пропажи.</summary>
    [Fact]
    public async Task Cli_return_with_disconnected_answer_closes_the_session_at_the_last_live_poll()
    {
        var t = Make();
        var (liveFrom, liveTo) = await LivePoll(t);

        await LoseAndReturn(t, VpnStatus.Disconnected);

        Assert.Null(t.History.Active);
        Assert.InRange(Assert.Single(t.History.Previous).EndedAt!.Value, liveFrom, liveTo);
    }

    [Fact]
    public async Task Cli_return_with_the_same_city_continues_the_session()
    {
        var t = Make();
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
        var startedAt = t.History.Active!.StartedAt;

        await LoseAndReturn(t, Amsterdam);

        Assert.Equal(ConnectionState.Connected, t.Store.Current.Connection);
        Assert.Equal("TUN", t.Store.Current.Mode);
        Assert.Empty(t.History.Previous);
        Assert.Equal(startedAt, t.History.Active?.StartedAt);
    }

    [Fact]
    public async Task Cli_return_with_another_city_closes_the_old_session_at_the_last_live_poll()
    {
        var t = Make();
        var (liveFrom, liveTo) = await LivePoll(t);

        await LoseAndReturn(t, Paris);

        var closed = Assert.Single(t.History.Previous);
        Assert.Equal("AMSTERDAM", closed.City);
        Assert.InRange(closed.EndedAt!.Value, liveFrom, liveTo);
        Assert.Equal("PARIS", t.History.Active?.City);
    }

    /// <summary>`disconnect` упал с ENOENT: отключения не было, туннель мог остаться.</summary>
    [Fact]
    public async Task Disconnect_without_cli_keeps_the_session_open()
    {
        var t = Make();
        var (liveFrom, liveTo) = await LivePoll(t);
        var session = t.History.Active;

        t.Vpn.Missing = true;
        await t.Coord.DisconnectAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.CliMissing, t.Store.Current.Connection);
        Assert.Equal(session, t.History.Active);
        Assert.Empty(t.History.Previous);

        // CLI вернулся, туннеля нет: закрываем последним живым опросом, а не моментом возврата.
        await Task.Delay(20, TestContext.Current.CancellationToken);
        t.Vpn.Missing = false;
        t.Vpn.Status = VpnStatus.Disconnected;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.InRange(Assert.Single(t.History.Previous).EndedAt!.Value, liveFrom, liveTo);
    }

    // --- Старт без CLI: активная сессия поднята с диска ---

    private static readonly DateTimeOffset RestoredStart = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RestoredLastSeen = RestoredStart.AddMinutes(30);

    private static async Task<Rig> StartWithoutCliAndRestoredSession()
    {
        var t = Make(restored: new ActiveSessionState(
            new ConnectionHistoryEntry("AMSTERDAM", "", 0, RestoredStart, EndedAt: null), RestoredLastSeen));
        t.Vpn.Missing = true;
        for (var i = 0; i < 3; i++)
            await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.CliMissing, t.Store.Current.Connection);
        Assert.Equal(RestoredStart, t.History.Active?.StartedAt);
        Assert.Empty(t.History.Previous);
        return t;
    }

    [Fact]
    public async Task Restored_session_without_cli_closes_at_its_last_seen_when_cli_says_disconnected()
    {
        var t = await StartWithoutCliAndRestoredSession();

        t.Vpn.Missing = false;
        t.Vpn.Status = VpnStatus.Disconnected;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Null(t.History.Active);
        Assert.Equal(RestoredLastSeen, Assert.Single(t.History.Previous).EndedAt);
    }

    [Fact]
    public async Task Restored_session_without_cli_continues_when_cli_returns_with_the_same_city()
    {
        var t = await StartWithoutCliAndRestoredSession();

        t.Vpn.Missing = false;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Empty(t.History.Previous);
        Assert.Equal(RestoredStart, t.History.Active?.StartedAt);
    }

    [Fact]
    public async Task Restored_session_without_cli_closes_at_its_last_seen_when_cli_returns_with_another_city()
    {
        var t = await StartWithoutCliAndRestoredSession();

        t.Vpn.Missing = false;
        t.Vpn.Status = Paris;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        var closed = Assert.Single(t.History.Previous);
        Assert.Equal(RestoredStart, closed.StartedAt);
        Assert.Equal(RestoredLastSeen, closed.EndedAt);
        Assert.Equal("PARIS", t.History.Active?.City);
    }

    /// <summary>В Error CLI есть, но не ответил: туннель обычно жив, и сессию закрывать нельзя.</summary>
    [Fact]
    public async Task Error_keeps_the_session_and_the_tunnel_data()
    {
        var t = Make();
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
        var session = t.History.Active;
        Assert.NotNull(session);

        t.Vpn.Fail = true;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Error, t.Store.Current.Connection);
        Assert.Equal(session, t.History.Active);
        Assert.Empty(t.History.Previous);
        Assert.Equal("AMSTERDAM", t.Store.Current.Location);
        Assert.Equal("TUN", t.Store.Current.Mode);
    }

    /// <summary>Пропажа CLI — не обрыв связи: ни событий связи, ни плашки «связь потеряна».</summary>
    [Fact]
    public async Task Cli_loss_while_connected_is_not_reported_as_a_link_drop()
    {
        var notifier = new FakeNotifier();
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        var settings = new NotificationsStore(
            Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "notifications"));
        var alerts = new ConnectionAlertService(notifier, settings, clock: () => now);
        var t = Make(alerts);
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await alerts.LastNotifyTask;

        t.Vpn.Missing = true;
        for (var i = 0; i < 5; i++)
        {
            now += TimeSpan.FromSeconds(4);
            await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
            await alerts.LastNotifyTask;
        }

        Assert.Equal([LinkEvent.Up], t.Link.Events);
        Assert.Empty(notifier.Sent);
    }

    /// <summary>Последний живой опрос «подключено» и окно времени, в которое он попал.</summary>
    private static async Task<(DateTimeOffset From, DateTimeOffset To)> LivePoll(Rig t)
    {
        var from = DateTimeOffset.UtcNow;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
        var to = DateTimeOffset.UtcNow;
        Assert.NotNull(t.History.Active);
        return (from, to);
    }

    /// <summary>CLI пропадает на пару опросов и возвращается с ответом <paramref name="answer"/>.</summary>
    private static async Task LoseAndReturn(Rig t, VpnStatus answer)
    {
        // Паузы разводят во времени последний живой опрос, пропажу и возврат CLI.
        await Task.Delay(20, TestContext.Current.CancellationToken);
        t.Vpn.Missing = true;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);

        t.Vpn.Missing = false;
        t.Vpn.Status = answer;
        await t.Coord.PollOnceAsync(TestContext.Current.CancellationToken);
    }

    private sealed record Rig(
        VpnCoordinator Coord, CliLossVpnService Vpn, AppStateStore Store,
        ConnectionHistoryTracker History, SettableTrafficReader Traffic, RecordingLinkObserver Link);

    /// <param name="restored">Активная сессия на диске до создания трекера: так выглядит старт приложения.</param>
    private static Rig Make(ILinkObserver? alerts = null, ActiveSessionState? restored = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var activeStore = new ActiveSessionStore(Path.Combine(dir, "connection-active"));
        if (restored is not null) activeStore.Save(restored);

        var vpn = new CliLossVpnService { Status = Amsterdam };
        var store = new AppStateStore();
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")), activeStore);
        var reader = new SettableTrafficReader();
        var link = new RecordingLinkObserver(alerts);
        var coord = new VpnCoordinator(
            vpn, new TrafficMonitor(reader), store, history,
            new IpVersionStore(Path.Combine(dir, "ip-version")), new FakeAuthService(),
            new LastLocationStore(Path.Combine(dir, "last-location")), link: link);
        return new Rig(coord, vpn, store, history, reader, link);
    }

    private sealed class SettableTrafficReader : ITrafficReader
    {
        public (long rx, long tx) Counters { get; set; } = (1000, 100);
        public (long rx, long tx)? Read(string iface) => Counters;
    }

    /// <summary>Команды отвечают заданным статусом, падают (CLI молчит) или не находят файл CLI.</summary>
    private sealed class CliLossVpnService : IVpnService
    {
        public VpnStatus Status { get; set; } = VpnStatus.Disconnected;
        public bool Fail { get; set; }
        public bool Missing { get; set; }

        public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
        {
            ThrowIfMissing();
            if (Fail) throw new InvalidOperationException("CLI молчит");
            return Task.FromResult(Status);
        }

        public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
            IpVersionPreference ipVersion = IpVersionPreference.Auto,
            bool killSwitch = false, CancellationToken ct = default)
        {
            ThrowIfMissing();
            return Task.FromResult(Status);
        }

        public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default)
        {
            ThrowIfMissing();
            return Task.FromResult(VpnStatus.Disconnected);
        }

        public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Location>>([]);

        public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
            Task.FromResult(new License("", "", 0, null));

        public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>("test");

        private void ThrowIfMissing()
        {
            if (Missing)
                throw new CliMissingException(
                    new AppError(AppErrorCode.CliMissing, "adguardvpn-cli"),
                    new System.ComponentModel.Win32Exception(2));
        }
    }
}
