using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vantah.Core.Auth;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;

namespace Vantah.App.Services;

public sealed class VpnCoordinator(
    IVpnService vpn,
    TrafficMonitor traffic,
    AppStateStore store,
    ConnectionHistoryTracker history,
    IpVersionStore ipVersionStore,
    IAuthService auth,
    LastLocationStore? lastLocation = null)
{
    private DateTime _lastPollUtc = DateTime.UtcNow;
    // _opGate сериализует операции; _operationInFlight гейтит опрос — самодостаточен,
    // не завязан на счётчик семафора.
    private readonly SemaphoreSlim _opGate = new(1, 1);
    private volatile bool _operationInFlight;
    private int _pollInFlight;
    private volatile IReadOnlyList<Location> _knownLocations = Array.Empty<Location>();

    /// <summary>Список известных локаций для обогащения истории Country/Ping (город → страна/пинг).</summary>
    public void UpdateKnownLocations(IReadOnlyList<Location> locations) => _knownLocations = locations;

    /// <summary>Завершённые сессии для UI (newest-first, cap 12).</summary>
    public IReadOnlyList<ConnectionHistoryEntry> PreviousConnections => history.Previous;

    /// <summary>Зонд состояния логина (через `license`). Обновляет снапшот, чтобы UI гейтил
    /// форму входа. Ошибки глотаем: CLI/сеть недоступны — не роняем UI, оставляем прежнее.</summary>
    public async Task RefreshLoginStateAsync(CancellationToken ct = default)
    {
        try
        {
            var state = await auth.GetLoginStateAsync(ct);
            store.Set(s => s with { LoginState = state });
        }
        catch { /* оставляем прежнее состояние */ }
    }

    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        // Во время connect/disconnect (CLI может работать долго) опрос
        // не должен перетереть состояние обратно в Disconnected.
        if (_operationInFlight) return;

        // Single-flight: не даём двум опросам перекрыться (напр. таймер тикнул раньше,
        // чем предыдущий опрос успел завершиться).
        if (Interlocked.Exchange(ref _pollInFlight, 1) == 1) return;
        try
        {
            // Держим состояние логина в актуальном виде: форма входа появляется/исчезает сама.
            await RefreshLoginStateAsync(ct);

            try
            {
                var status = await vpn.GetStatusAsync(ct);
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastPollUtc).TotalSeconds;
                _lastPollUtc = now;

                TrafficSample? sample = null;
                if (status.IsConnected && status.Interface is { } iface)
                    sample = traffic.Poll(iface, elapsed);
                else
                    traffic.Reset();

                TrackHistory(status);

                store.Set(s => s with
                {
                    Connection = status.IsConnected ? ConnectionState.Connected : ConnectionState.Disconnected,
                    Location = status.Location,
                    LocationDisplay = ResolveLocationDisplay(status),
                    Mode = status.Mode,
                    Interface = status.Interface,
                    Traffic = sample,
                    Error = null,
                });
            }
            catch (Exception ex)
            {
                store.Set(s => s with { Connection = ConnectionState.Error, Error = ex.Message });
            }
        }
        finally
        {
            Interlocked.Exchange(ref _pollInFlight, 0);
        }
    }

    public async Task ConnectAsync(string? location, bool fastest, CancellationToken ct = default)
    {
        // Семафор сериализует connect/disconnect: два быстрых клика не запустят
        // две параллельные операции CLI, второй вызов дождётся завершения первого.
        await _opGate.WaitAsync(ct);
        try
        {
            _operationInFlight = true;
            store.Set(s => s with { Connection = ConnectionState.Connecting, Error = null });
            try
            {
                var status = await vpn.ConnectAsync(location, fastest, ipVersionStore.Load(), ct);
                TrackHistory(status);
                if (status.IsConnected)
                {
                    try { lastLocation?.Save(location ?? status.Location); }
                    catch { /* best-effort persist, не роняем состояние подключения */ }
                }
                store.Set(s => s with
                {
                    Connection = status.IsConnected ? ConnectionState.Connected : ConnectionState.Disconnected,
                    Location = status.Location, LocationDisplay = ResolveLocationDisplay(status),
                    Mode = status.Mode, Interface = status.Interface, Error = null,
                });
            }
            catch (OperationCanceledException)
            {
                // Отмена — не ошибка: состояние осознанно остаётся Connecting, следующий тик
                // PollOnceAsync перепишет его реальным GetStatus; в Error не уходим намеренно.
                throw;
            }
            catch (Exception ex)
            {
                store.Set(s => s with { Connection = ConnectionState.Error, Error = ex.Message });
            }
        }
        finally
        {
            _operationInFlight = false;
            _opGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        // Семафор сериализует connect/disconnect: см. комментарий в ConnectAsync.
        await _opGate.WaitAsync(ct);
        try
        {
            _operationInFlight = true;
            store.Set(s => s with { Connection = ConnectionState.Disconnecting, Error = null });
            try
            {
                await vpn.DisconnectAsync(ct);
                traffic.Reset();
                history.OnDisconnected(DateTimeOffset.UtcNow);
                store.Set(s => s with { Connection = ConnectionState.Disconnected, Location = null, LocationDisplay = null, Interface = null, Traffic = null });
            }
            catch (OperationCanceledException)
            {
                // Отмена — не ошибка: состояние осознанно остаётся Disconnecting, следующий тик
                // PollOnceAsync перепишет его реальным GetStatus; в Error не уходим намеренно.
                throw;
            }
            catch (Exception ex)
            {
                store.Set(s => s with { Connection = ConnectionState.Error, Error = ex.Message });
            }
        }
        finally
        {
            _operationInFlight = false;
            _opGate.Release();
        }
    }

    private void TrackHistory(VpnStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        if (status.IsConnected && !string.IsNullOrWhiteSpace(status.Location))
        {
            var (city, country, ping) = ResolveLocation(status.Location);
            history.OnConnected(city, country, ping, now);
        }
        else
        {
            history.OnDisconnected(now);
        }
    }

    // Человекочитаемая локация «Город, Страна» из известного списка (или просто город).
    private string? ResolveLocationDisplay(VpnStatus status)
    {
        if (!status.IsConnected || string.IsNullOrWhiteSpace(status.Location)) return null;
        var (city, country, _) = ResolveLocation(status.Location);
        return string.IsNullOrEmpty(country) ? city : $"{city}, {country}";
    }

    // adguardvpn-cli status отдаёт только город (в верхнем регистре). Country/Ping берём
    // из известного списка локаций по совпадению города без учёта регистра; иначе — fallback.
    private (string City, string Country, int Ping) ResolveLocation(string city)
    {
        foreach (var l in _knownLocations)
            if (string.Equals(l.City, city, StringComparison.OrdinalIgnoreCase))
                return (l.City, l.Country, l.PingMs);
        return (city, "", 0);
    }
}
