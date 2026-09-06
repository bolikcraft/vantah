using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
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

    /// <summary>Сбой опроса — не обрыв: `status` подвисает и во время восстановления связи.</summary>
    [Fact]
    public async Task Failed_poll_reports_nothing()
    {
        var vpn = new FakeLinkVpnService(new VpnStatus(true, "AMSTERDAM", "TUN", "tun0"));
        var (coord, link) = Make(vpn);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        vpn.Fail = true;
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal([LinkEvent.Up], link.Events);
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

    private static (VpnCoordinator Coordinator, RecordingLinkObserver Link) Make(IVpnService vpn)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var link = new RecordingLinkObserver();
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

/// <summary>Приёмник событий связи: запоминает порядок, ничего не решает.</summary>
internal sealed class RecordingLinkObserver : ILinkObserver
{
    public List<LinkEvent> Events { get; } = [];

    public void Observe(LinkEvent link) => Events.Add(link);
}

/// <summary>Дубль VPN-сервиса с настраиваемым статусом и переключаемым сбоем.</summary>
internal sealed class FakeLinkVpnService(VpnStatus status) : IVpnService
{
    public VpnStatus Status { get; set; } = status;
    public bool Fail { get; set; }

    public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
        IpVersionPreference ipVersion = IpVersionPreference.Auto,
        bool killSwitch = false, CancellationToken ct = default) => Task.FromResult(Status);

    public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) =>
        Fail ? Task.FromException<VpnStatus>(new InvalidOperationException("CLI молчит"))
             : Task.FromResult(Status);

    public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Location>>([]);

    public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
        Task.FromResult(VpnStatus.Disconnected);

    public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
        Task.FromResult(new License("", "", 0, null));

    public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>("test");
}
