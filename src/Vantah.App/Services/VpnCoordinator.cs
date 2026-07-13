using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    ConnectionHistoryTracker history)
{
    private DateTime _lastPollUtc = DateTime.UtcNow;
    private volatile bool _operationInFlight;
    private volatile IReadOnlyList<Location> _knownLocations = Array.Empty<Location>();

    /// <summary>Список известных локаций для обогащения истории Country/Ping (город → страна/пинг).</summary>
    public void UpdateKnownLocations(IReadOnlyList<Location> locations) => _knownLocations = locations;

    /// <summary>Завершённые сессии для UI (newest-first, cap 12).</summary>
    public IReadOnlyList<ConnectionHistoryEntry> PreviousConnections => history.Previous;

    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        // Во время connect/disconnect (CLI может работать долго) опрос
        // не должен перетереть состояние обратно в Disconnected.
        if (_operationInFlight) return;

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

    public async Task ConnectAsync(string? location, bool fastest, CancellationToken ct = default)
    {
        _operationInFlight = true;
        store.Set(s => s with { Connection = ConnectionState.Connecting, Error = null });
        try
        {
            var status = await vpn.ConnectAsync(location, fastest, ct);
            TrackHistory(status);
            store.Set(s => s with
            {
                Connection = status.IsConnected ? ConnectionState.Connected : ConnectionState.Disconnected,
                Location = status.Location, Mode = status.Mode, Interface = status.Interface, Error = null,
            });
        }
        catch (Exception ex)
        {
            store.Set(s => s with { Connection = ConnectionState.Error, Error = ex.Message });
        }
        finally
        {
            _operationInFlight = false;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _operationInFlight = true;
        store.Set(s => s with { Connection = ConnectionState.Disconnecting, Error = null });
        try
        {
            await vpn.DisconnectAsync(ct);
            traffic.Reset();
            history.OnDisconnected(DateTimeOffset.UtcNow);
            store.Set(s => s with { Connection = ConnectionState.Disconnected, Location = null, Interface = null, Traffic = null });
        }
        catch (Exception ex)
        {
            store.Set(s => s with { Connection = ConnectionState.Error, Error = ex.Message });
        }
        finally
        {
            _operationInFlight = false;
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
