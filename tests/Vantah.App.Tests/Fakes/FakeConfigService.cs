using Vantah.Core.Models;
using Vantah.Core.Settings;

namespace Vantah.App.Tests.Fakes;

/// <summary>Сервис-обманка настроек CLI: отдаёт заданный конфиг и записывает, что у него просили.</summary>
public sealed class FakeConfigService(VpnConfig? current = null) : IConfigService
{
    private VpnConfig _current = current ?? new VpnConfig();

    // Ворота для `config show`: пока взведены, чтение конфига «висит» — так тест видит
    // вкладку в состоянии загрузки. По умолчанию не взведены, ответ синхронный, как раньше.
    private TaskCompletionSource? _getGate;

    /// <summary>Имена вызванных операций по порядку: «get», «set-mode:socks», …</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Чем падает чтение конфига; null — читается успешно.</summary>
    public Exception? GetError { get; set; }

    /// <summary>Задержать ответ на чтение конфига до <see cref="ReleaseGet"/>.</summary>
    public void HoldGet() =>
        _getGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Отпустить задержанное чтение конфига.</summary>
    public void ReleaseGet()
    {
        var gate = _getGate;
        _getGate = null;
        gate?.TrySetResult();
    }

    private Task<VpnConfig> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(_current);
    }

    public async Task<VpnConfig> GetAsync(CancellationToken ct = default)
    {
        if (_getGate is { } gate) await gate.Task;
        Calls.Add("get");
        if (GetError is not null) throw GetError;
        return _current;
    }

    public Task<VpnConfig> SetModeAsync(VpnMode mode, CancellationToken ct = default)
    {
        _current = _current with { Mode = mode };
        return Record($"set-mode:{mode}");
    }

    public Task<VpnConfig> SetSocksPortAsync(int port, CancellationToken ct = default) =>
        Record($"set-socks-port:{port}");

    public Task<VpnConfig> SetSocksHostAsync(string host, CancellationToken ct = default) =>
        Record($"set-socks-host:{host}");

    public Task<VpnConfig> SetSocksUsernameAsync(string username, CancellationToken ct = default) =>
        Record($"set-socks-username:{username}");

    public Task<VpnConfig> SetSocksPasswordAsync(string password, CancellationToken ct = default) =>
        Record($"set-socks-password:{password}");

    public Task<VpnConfig> ClearSocksAuthAsync(CancellationToken ct = default) =>
        Record("clear-socks-auth");

    public Task<VpnConfig> SetDnsAsync(string upstream, CancellationToken ct = default)
    {
        _current = _current with { DnsUpstream = upstream };
        return Record($"set-dns:{upstream}");
    }

    public Task<VpnConfig> ResetDnsAsync(CancellationToken ct = default)
    {
        _current = _current with { DnsUpstream = "" };
        return Record("reset-dns");
    }

    public Task<VpnConfig> SetChangeSystemDnsAsync(bool on, CancellationToken ct = default) =>
        Record($"set-change-system-dns:{on}");

    public Task<VpnConfig> SetTunRoutingModeAsync(TunnelRoutingMode mode, CancellationToken ct = default)
    {
        _current = _current with { TunnelRoutingMode = mode };
        return Record($"set-tun-routing-mode:{mode}");
    }

    public Task<VpnConfig> SetProtocolAsync(VpnProtocol protocol, CancellationToken ct = default) =>
        Record($"set-protocol:{protocol}");

    public Task<VpnConfig> SetPostQuantumAsync(bool on, CancellationToken ct = default) =>
        Record($"set-post-quantum:{on}");

    public Task<VpnConfig> SetUpdateChannelAsync(UpdateChannel channel, CancellationToken ct = default) =>
        Record($"set-update-channel:{channel}");

    public Task<VpnConfig> SetShowNotificationsAsync(bool on, CancellationToken ct = default) =>
        Record($"set-show-notifications:{on}");

    public Task<VpnConfig> SetDebugLoggingAsync(bool on, CancellationToken ct = default) =>
        Record($"set-debug-logging:{on}");

    public Task<VpnConfig> SetCrashReportingAsync(bool on, CancellationToken ct = default) =>
        Record($"set-crash-reporting:{on}");

    public Task<VpnConfig> SetTelemetryAsync(bool on, CancellationToken ct = default) =>
        Record($"set-telemetry:{on}");

    public Task<VpnConfig> SetShowHintsAsync(bool on, CancellationToken ct = default) =>
        Record($"set-show-hints:{on}");

    public Task<VpnConfig> SetBoundIfOverrideAsync(string iface, CancellationToken ct = default) =>
        Record($"set-bound-if-override:{iface}");

    public Task<string> CreateRouteScriptAsync(CancellationToken ct = default)
    {
        Calls.Add("create-route-script");
        return Task.FromResult("route script created");
    }
}
