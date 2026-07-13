using System.Text.RegularExpressions;
using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Settings;

/// <summary>Разбор вывода <c>adguardvpn-cli config show</c> в <see cref="VpnConfig"/>.</summary>
public static partial class ConfigParser
{
    // CLI показывает неизменённые настройки как «Default (X)» — нам нужен сам X.
    [GeneratedRegex(@"^Default\s*\((?<v>.*)\)$", RegexOptions.IgnoreCase)]
    private static partial Regex DefaultWrapRegex();

    public static VpnConfig Parse(string cliOutput)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in Ansi.Strip(cliOutput).Split('\n'))
        {
            var line = rawLine.Trim();
            // Режем по ПЕРВОМУ двоеточию: в DoH-апстриме («https://…») двоеточие есть и внутри значения.
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            map[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }

        return new VpnConfig
        {
            DataDirectory             = NullIfEmpty(Get(map, "Data directory")),
            Mode                      = ParseMode(Get(map, "Mode")),
            SocksPort                 = ParseInt(Get(map, "SOCKS port"), 1080),
            SocksHost                 = NullIfEmpty(Get(map, "SOCKS host")),
            SocksUsername             = NullIfEmpty(Get(map, "SOCKS username")),
            DnsUpstream               = Get(map, "DNS upstream") ?? "",
            TunnelRoutingMode         = ParseRouting(Get(map, "Tunnel routing mode")),
            ChangeSystemDns           = ParseBool(Get(map, "Change system DNS")),
            CrashReporting            = ParseBool(Get(map, "Crash reporting")),
            SendAnonymizedUsageData   = ParseBool(Get(map, "Send anonymized usage data")),
            UpdateChannel             = ParseChannel(Get(map, "Update channel")),
            Protocol                  = ParseProtocol(Get(map, "Protocol")),
            PostQuantum               = ParseBool(Get(map, "Post-quantum cryptography")),
            ShowHints                 = ParseBool(Get(map, "Show hints")),
            DebugLogging              = ParseBool(Get(map, "Debug logging")),
            ShowNotifications         = ParseBool(Get(map, "Show notifications")),
            OutboundInterfaceOverride = ParseOverride(Get(map, "Outbound interface override")),
        };
    }

    private static string? Get(IReadOnlyDictionary<string, string> m, string key) =>
        m.TryGetValue(key, out var v) ? v : null;

    private static string Unwrap(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "";
        var t = v.Trim();
        var m = DefaultWrapRegex().Match(t);
        return (m.Success ? m.Groups["v"].Value : t).Trim();
    }

    private static bool ParseBool(string? v) =>
        string.Equals(Unwrap(v), "on", StringComparison.OrdinalIgnoreCase);

    private static int ParseInt(string? v, int fallback) =>
        int.TryParse(Unwrap(v), out var n) ? n : fallback;

    private static string? NullIfEmpty(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static VpnMode ParseMode(string? v) =>
        string.Equals(Unwrap(v), "socks", StringComparison.OrdinalIgnoreCase) ? VpnMode.Socks : VpnMode.Tun;

    private static TunnelRoutingMode ParseRouting(string? v) => Unwrap(v).ToLowerInvariant() switch
    {
        "script" => TunnelRoutingMode.Script,
        "none"   => TunnelRoutingMode.None,
        _        => TunnelRoutingMode.Auto,
    };

    private static VpnProtocol ParseProtocol(string? v) => Unwrap(v).ToLowerInvariant() switch
    {
        "http2" => VpnProtocol.Http2,
        "quic"  => VpnProtocol.Quic,
        _       => VpnProtocol.Auto,
    };

    private static UpdateChannel ParseChannel(string? v) => Unwrap(v).ToLowerInvariant() switch
    {
        "beta"    => UpdateChannel.Beta,
        "nightly" => UpdateChannel.Nightly,
        _         => UpdateChannel.Release,
    };

    private static string? ParseOverride(string? v)
    {
        var u = Unwrap(v);
        return string.IsNullOrWhiteSpace(u) || u.Equals("not set", StringComparison.OrdinalIgnoreCase)
            ? null
            : u;
    }
}
