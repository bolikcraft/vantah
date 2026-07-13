namespace Vantah.Core.Models;

/// <summary>
/// Настройки adguardvpn-cli, как их показывает <c>config show</c>.
/// </summary>
public sealed record VpnConfig
{
    public string? DataDirectory { get; init; }
    public VpnMode Mode { get; init; } = VpnMode.Tun;
    public int SocksPort { get; init; } = 1080;
    public string? SocksHost { get; init; }
    public string? SocksUsername { get; init; }
    public string DnsUpstream { get; init; } = "";
    public TunnelRoutingMode TunnelRoutingMode { get; init; } = TunnelRoutingMode.Auto;
    public bool ChangeSystemDns { get; init; }
    public bool CrashReporting { get; init; }
    public bool SendAnonymizedUsageData { get; init; }
    public UpdateChannel UpdateChannel { get; init; } = UpdateChannel.Release;
    public VpnProtocol Protocol { get; init; } = VpnProtocol.Auto;
    public bool PostQuantum { get; init; }
    public bool ShowHints { get; init; }
    public bool DebugLogging { get; init; }
    public bool ShowNotifications { get; init; }
    public string? OutboundInterfaceOverride { get; init; }

    // "DNS upstream: Default (provided by AdGuard VPN)" — апстрим не задан, работают серверы AdGuard.
    public bool DnsIsDefault =>
        DnsUpstream.StartsWith("Default", StringComparison.OrdinalIgnoreCase);
}
