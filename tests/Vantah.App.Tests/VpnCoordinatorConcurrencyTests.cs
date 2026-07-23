using Avalonia.Headless.XUnit;
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
/// Регресс: <c>_operationInFlight</c> был обычным bool-флажком, а не мьютексом — два быстрых
/// клика по «Подключить» запускали два параллельных вызова CLI. Теперь ConnectAsync/DisconnectAsync
/// сериализованы через <see cref="SemaphoreSlim"/>, а отмена (OperationCanceledException) внутри
/// операции не трактуется как ошибка (не пишется ConnectionState.Error).
/// </summary>
public class VpnCoordinatorConcurrencyTests
{
    [AvaloniaFact]
    public async Task Concurrent_connects_do_not_overlap()
    {
        var vpn = new GatedVpn();
        var coord = MakeCoordinator(vpn, out _);

        var t1 = coord.ConnectAsync("Amsterdam", false);
        var t2 = coord.ConnectAsync("Berlin", false);

        // Отпускаем гейт — обе операции должны пройти по очереди, а не одновременно.
        vpn.Release();
        await Task.WhenAll(t1, t2);

        Assert.Equal(1, vpn.MaxConcurrent);
    }

    [AvaloniaFact]
    public async Task Cancelled_connect_does_not_set_error_state()
    {
        var vpn = new GatedVpn();
        var coord = MakeCoordinator(vpn, out var store);

        using var cts = new CancellationTokenSource();
        var t = coord.ConnectAsync("Amsterdam", false, cts.Token);

        cts.Cancel();
        vpn.Release();

        try { await t; }
        catch (OperationCanceledException) { /* ожидаемо */ }

        Assert.NotEqual(ConnectionState.Error, store.Current.Connection);
    }

    private static VpnCoordinator MakeCoordinator(IVpnService vpn, out AppStateStore store)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var lastLocation = new LastLocationStore(Path.Combine(dir, "last-location"));
        return new VpnCoordinator(vpn, traffic, store, history, ipStore, new FakeAuthService(), lastLocation);
    }

    /// <summary>
    /// Дубль VPN-сервиса: ConnectAsync блокируется до <see cref="Release"/> (общий TCS, который
    /// остаётся completed после первого Release — так проходят и последующие вызовы), считая
    /// пиковое число одновременных входов. Реагирует на CancellationToken — это и вызывает
    /// OperationCanceledException в тесте отмены.
    /// </summary>
    private sealed class GatedVpn : IVpnService
    {
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _lock = new();
        private int _current;

        public int MaxConcurrent { get; private set; }

        public void Release() => _gate.TrySetResult(true);

        public async Task<VpnStatus> ConnectAsync(string? location, bool fastest,
            IpVersionPreference ipVersion = IpVersionPreference.Auto,
            bool killSwitch = false, CancellationToken ct = default)
        {
            lock (_lock)
            {
                _current++;
                if (_current > MaxConcurrent) MaxConcurrent = _current;
            }
            try
            {
                await _gate.Task.WaitAsync(ct);
            }
            finally
            {
                lock (_lock) { _current--; }
            }
            return VpnStatus.Disconnected;
        }

        public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Location>>([]);
        public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
            Task.FromResult(new License("", "", 0, null));
        public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>("test");
    }
}
