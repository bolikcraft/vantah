using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.Core.Auth;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Регресс: опрос логина (`license`) ходит в сеть и при обрыве висит до таймаута 15 c. Пока он
/// шёл первым в тике, статус за это время не опрашивался вовсе — приложение пропускало и обрыв,
/// и восстановление (замерено на живом обрыве 03.09.2026).
/// </summary>
public class VpnCoordinatorLoginProbeTests
{
    [Fact]
    public async Task Unknown_login_state_is_awaited_on_the_first_poll()
    {
        var (coord, store, auth) = Make(new FakeStatusVpnService(new VpnStatus(true, "OSLO", "TUN", "tun0")));
        Assert.Equal(LoginState.Unknown, store.Current.LoginState);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        // Состояние логина нужно немедленно: по нему гейтится форма входа и строится план
        // автоподключения на старте.
        Assert.Equal(LoginState.LoggedIn, store.Current.LoginState);
        Assert.Equal(1, auth.LoginStateCalls);
    }

    [Fact]
    public async Task Slow_login_probe_does_not_delay_the_status_poll()
    {
        var vpn = new FakeStatusVpnService(new VpnStatus(true, "OSLO", "TUN", "tun0"));
        var (coord, store, auth) = Make(vpn, TimeSpan.Zero);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);   // логин стал известен

        // Дальше зонд логина висит (как `license` без сети), а статус обязан обновиться.
        auth.Gate = new TaskCompletionSource();
        vpn.Status = VpnStatus.Reconnecting("OSLO", "TUN", "tun0");

        // Таймаут обязателен: с регрессом (логин снова ждут в тике) опрос повис бы навсегда,
        // а тест должен падать, а не висеть.
        await coord.PollOnceAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Connecting, store.Current.Connection);
        Assert.False(coord.LoginProbeTask.IsCompleted);   // зонд всё ещё висит
        auth.Gate.SetResult();
        await coord.LoginProbeTask;
    }

    [Fact]
    public async Task Login_is_probed_at_most_once_per_period()
    {
        var (coord, _, auth) = Make(new FakeStatusVpnService(new VpnStatus(true, "OSLO", "TUN", "tun0")));

        for (var i = 0; i < 5; i++)
        {
            await coord.PollOnceAsync(TestContext.Current.CancellationToken);
            await coord.LoginProbeTask;
        }

        // Первый тик спросил синхронно, остальные уложились в период — вход меняется редко.
        Assert.Equal(1, auth.LoginStateCalls);
    }

    private static (VpnCoordinator Coordinator, AppStateStore Store, SlowAuthService Auth) Make(
        IVpnService vpn, TimeSpan? loginProbePeriod = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var auth = new SlowAuthService();
        var coord = new VpnCoordinator(vpn, traffic, store, history,
            new IpVersionStore(Path.Combine(dir, "ip-version")), auth,
            new LastLocationStore(Path.Combine(dir, "last-location")),
            loginProbePeriod: loginProbePeriod);
        return (coord, store, auth);
    }

    /// <summary>Логин, который умеет висеть: <see cref="Gate"/> держит ответ, пока его не отпустят.</summary>
    private sealed class SlowAuthService : IAuthService
    {
        public int LoginStateCalls { get; private set; }
        public TaskCompletionSource? Gate { get; set; }

        public async Task<LoginState> GetLoginStateAsync(CancellationToken ct = default)
        {
            LoginStateCalls++;
            if (Gate is { } gate) await gate.Task.WaitAsync(ct);
            return LoginState.LoggedIn;
        }

        public Task<LoginResult> LoginAsync(Action<DeviceCodePrompt> onPrompt, CancellationToken ct = default) =>
            Task.FromResult(LoginResult.Ok());

        public Task LogoutAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}

file sealed class FakeStatusVpnService(VpnStatus status) : IVpnService
{
    public VpnStatus Status { get; set; } = status;

    public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
        IpVersionPreference ipVersion = IpVersionPreference.Auto,
        bool killSwitch = false, CancellationToken ct = default) => Task.FromResult(Status);

    public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(Status);

    public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) => Task.FromResult(VpnStatus.Disconnected);

    public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Location>>([]);

    public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
        Task.FromResult(new License("", "UNKNOWN", 0, null));

    public Task<string?> GetCliVersionAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
}
