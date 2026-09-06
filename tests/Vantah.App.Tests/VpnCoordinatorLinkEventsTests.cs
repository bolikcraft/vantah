using Vantah.App.Localization;
using Vantah.App.Notifications;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.Tests.Notifications;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.Notifications;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Координатор — единственное место, где обрыв отличим от отключения по кнопке: ответы CLI
/// он помечает как события связи, а команды пользователя — как свои. Без этого различия
/// уведомления пришлось бы гадать по таймингу.
/// </summary>
public class VpnCoordinatorLinkEventsTests
{
    [Fact]
    public async Task Poll_reports_the_phase_of_the_cli_answer()
    {
        var vpn = new FakeLinkVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, link) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        vpn.Status = VpnStatus.Reconnecting("AMSTERDAM", "TUN", "tun0");
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        vpn.Status = VpnStatus.Disconnected;
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal([LinkEvent.Up, LinkEvent.Retrying, LinkEvent.Down], link.Events);
    }

    /// <summary>«VPN is starting» — это ещё подъём туннеля, а не обрыв: событий не шлём.</summary>
    [Fact]
    public async Task Poll_that_sees_starting_reports_nothing()
    {
        var vpn = new FakeLinkVpnService(VpnStatus.Starting);
        var (coord, link) = Make(vpn);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Empty(link.Events);
    }

    /// <summary>Одиночный сбой — ещё не обрыв: медленный ответ CLI бывает и на живом туннеле.</summary>
    [Fact]
    public async Task A_single_failed_poll_reports_nothing()
    {
        var vpn = new FakeLinkVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, link) = Make(vpn);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        vpn.Fail = true;
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal([LinkEvent.Up], link.Events);
    }

    /// <summary>
    /// Обрыв с kill switch: `status` уходит в recovery и висит до таймаута, ответов больше нет.
    /// Молчание с порога считаем обрывом, иначе самый частый разрыв прошёл бы незамеченным.
    /// </summary>
    [Fact]
    public async Task Poll_failures_that_reach_the_threshold_report_a_drop()
    {
        var vpn = new FakeLinkVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, link) = Make(vpn);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        vpn.Fail = true;
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal([LinkEvent.Up, LinkEvent.Retrying, LinkEvent.Retrying], link.Events);
    }

    /// <summary>Успешный ответ обнуляет счётчик: разрозненные промахи в обрыв не складываются.</summary>
    [Fact]
    public async Task A_successful_poll_resets_the_failure_counter()
    {
        var vpn = new FakeLinkVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, link) = Make(vpn);

        for (var i = 0; i < 3; i++)
        {
            vpn.Fail = true;
            await coord.PollOnceAsync(TestContext.Current.CancellationToken);
            vpn.Fail = false;
            await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal([LinkEvent.Up, LinkEvent.Up, LinkEvent.Up], link.Events);
    }

    /// <summary>
    /// Опрос мог стартовать до нажатия «Подключить» и упасть уже после: пока команда
    /// пользователя в полёте, его сбой обрывом не считаем — иначе кнопка даст ложную плашку.
    /// </summary>
    [Fact]
    public async Task Poll_that_fails_during_a_user_command_reports_nothing()
    {
        var vpn = new FakeLinkVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, link) = Make(vpn);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        vpn.Fail = true;
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        // Второй опрос висит, как настоящий `status`; пользователь тем временем жмёт «Подключить».
        vpn.StatusGate = new TaskCompletionSource();
        var poll = coord.PollOnceAsync(TestContext.Current.CancellationToken);
        vpn.ConnectGate = new TaskCompletionSource();
        var connect = coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);
        await vpn.ConnectEntered.Task;
        vpn.StatusGate.SetResult();
        await poll;
        vpn.ConnectGate.SetResult();
        await connect;

        Assert.Equal([LinkEvent.Up, LinkEvent.UserConnect], link.Events);
    }

    /// <summary>
    /// Сквозная проверка: обрыв «по тишине» доходит до плашки, а вернувшийся туннель — до
    /// плашки о восстановлении. Окно устаканивания в решателе стоит на пути только первого
    /// успешного ответа, дальше восстановление объявляется.
    /// </summary>
    [Fact]
    public async Task Silent_drop_and_the_recovery_after_it_reach_the_notifier()
    {
        var vpn = new FakeLinkVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var notifier = new FakeNotifier();
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var settings = new NotificationsStore(
            Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "notifications"));
        var alerts = new ConnectionAlertService(notifier, settings, clock: () => now);
        var (coord, _) = Make(vpn, alerts);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await alerts.LastNotifyTask;

        // Ответов больше нет: каждый подвисший опрос съедает около 15 секунд.
        vpn.Fail = true;
        for (var i = 0; i < 4; i++)
        {
            now += TimeSpan.FromSeconds(15);
            await coord.PollOnceAsync(TestContext.Current.CancellationToken);
            await alerts.LastNotifyTask;
        }
        Assert.Equal([Localizer.Instance[LocKeys.Notify_ConnectionLost]], notifier.Sent.Select(s => s.Summary));

        // CLI снова отвечает «Connected».
        vpn.Fail = false;
        for (var i = 0; i < 3; i++)
        {
            now += TimeSpan.FromSeconds(4);
            await coord.PollOnceAsync(TestContext.Current.CancellationToken);
            await alerts.LastNotifyTask;
        }

        Assert.Equal(
        [
            Localizer.Instance[LocKeys.Notify_ConnectionLost],
            Localizer.Instance[LocKeys.Notify_ConnectionRestored],
        ], notifier.Sent.Select(s => s.Summary));
    }

    [Fact]
    public async Task Disconnect_marks_the_drop_as_the_users_own()
    {
        var vpn = new FakeLinkVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, link) = Make(vpn);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        await coord.DisconnectAsync(TestContext.Current.CancellationToken);
        vpn.Status = VpnStatus.Disconnected;
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal([LinkEvent.Up, LinkEvent.UserDisconnect, LinkEvent.Down], link.Events);
    }

    [Fact]
    public async Task Connect_marks_the_rise_as_the_users_own()
    {
        var vpn = new FakeLinkVpnService(VpnStatus.Disconnected);
        var (coord, link) = Make(vpn);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        vpn.Status = new VpnStatus(true, "AMSTERDAM", "TUN", "tun0");
        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal([LinkEvent.Down, LinkEvent.UserConnect, LinkEvent.Up], link.Events);
    }

    private static (VpnCoordinator Coordinator, RecordingLinkObserver Link) Make(
        IVpnService vpn, ILinkObserver? also = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var link = new RecordingLinkObserver(also);
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var coord = new VpnCoordinator(
            vpn, new TrafficMonitor(new FakeTrafficReader()), new AppStateStore(), history,
            new IpVersionStore(Path.Combine(dir, "ip-version")), new FakeAuthService(),
            new LastLocationStore(Path.Combine(dir, "last-location")), link: link);
        return (coord, link);
    }
}

/// <summary>Приёмник событий связи: запоминает порядок и по желанию передаёт событие дальше.</summary>
internal sealed class RecordingLinkObserver(ILinkObserver? next = null) : ILinkObserver
{
    public List<LinkEvent> Events { get; } = [];

    public void Observe(LinkEvent link)
    {
        Events.Add(link);
        next?.Observe(link);
    }
}

/// <summary>Дубль VPN-сервиса с настраиваемым статусом и переключаемым сбоем.</summary>
internal sealed class FakeLinkVpnService(VpnStatus status) : IVpnService
{
    public VpnStatus Status { get; set; } = status;
    public bool Fail { get; set; }

    /// <summary>Задержка ответа `status` — ею воспроизводим подвисший опрос.</summary>
    public TaskCompletionSource? StatusGate { get; set; }

    /// <summary>Задержка `connect` — ею держим команду пользователя в полёте.</summary>
    public TaskCompletionSource? ConnectGate { get; set; }

    /// <summary>Взводится, когда `connect` начался: тест дожидается операции, а не спит.</summary>
    public TaskCompletionSource ConnectEntered { get; } = new();

    public async Task<VpnStatus> ConnectAsync(string? location, bool fastest,
        IpVersionPreference ipVersion = IpVersionPreference.Auto,
        bool killSwitch = false, CancellationToken ct = default)
    {
        ConnectEntered.TrySetResult();
        if (ConnectGate is { } gate) await gate.Task;
        return Status;
    }

    public async Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (StatusGate is { } gate) await gate.Task;
        if (Fail) throw new InvalidOperationException("CLI молчит");
        return Status;
    }

    public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Location>>([]);

    public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
        Task.FromResult(VpnStatus.Disconnected);

    public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
        Task.FromResult(new License("", "", 0, null));

    public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>("test");
}
