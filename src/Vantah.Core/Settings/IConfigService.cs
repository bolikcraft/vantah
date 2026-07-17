using Vantah.Core.Models;

namespace Vantah.Core.Settings;

/// <summary>Чтение и запись настроек adguardvpn-cli. Каждая запись подтверждается перечитыванием.</summary>
public interface IConfigService
{
    Task<VpnConfig> GetAsync(CancellationToken ct = default);

    Task<VpnConfig> SetModeAsync(VpnMode mode, CancellationToken ct = default);
    Task<VpnConfig> SetSocksPortAsync(int port, CancellationToken ct = default);
    Task<VpnConfig> SetSocksHostAsync(string host, CancellationToken ct = default);
    Task<VpnConfig> SetSocksUsernameAsync(string username, CancellationToken ct = default);
    Task<VpnConfig> SetSocksPasswordAsync(string password, CancellationToken ct = default);
    Task<VpnConfig> ClearSocksAuthAsync(CancellationToken ct = default);
    Task<VpnConfig> SetDnsAsync(string upstream, CancellationToken ct = default);
    Task<VpnConfig> ResetDnsAsync(CancellationToken ct = default);
    Task<VpnConfig> SetChangeSystemDnsAsync(bool on, CancellationToken ct = default);
    Task<VpnConfig> SetTunRoutingModeAsync(TunnelRoutingMode mode, CancellationToken ct = default);
    Task<VpnConfig> SetProtocolAsync(VpnProtocol protocol, CancellationToken ct = default);
    Task<VpnConfig> SetPostQuantumAsync(bool on, CancellationToken ct = default);
    Task<VpnConfig> SetUpdateChannelAsync(UpdateChannel channel, CancellationToken ct = default);
    Task<VpnConfig> SetShowNotificationsAsync(bool on, CancellationToken ct = default);
    Task<VpnConfig> SetDebugLoggingAsync(bool on, CancellationToken ct = default);
    Task<VpnConfig> SetCrashReportingAsync(bool on, CancellationToken ct = default);
    Task<VpnConfig> SetTelemetryAsync(bool on, CancellationToken ct = default);
    Task<VpnConfig> SetShowHintsAsync(bool on, CancellationToken ct = default);
}
