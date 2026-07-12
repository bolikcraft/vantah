using System;
using System.Threading;
using System.Threading.Tasks;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;

namespace Vantah.App.Services;

public sealed class VpnCoordinator(
    IVpnService vpn,
    TrafficMonitor traffic,
    AppStateStore store)
{
    private DateTime _lastPollUtc = DateTime.UtcNow;
    private volatile bool _operationInFlight;

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
}
