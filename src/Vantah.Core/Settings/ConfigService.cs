using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Settings;

public sealed class ConfigCommandException(string message) : Exception(message);

/// <summary>
/// Настройки поверх <c>adguardvpn-cli config</c>. Пишем одной подкомандой <c>set-*</c>, затем
/// перечитываем <c>show</c>: возвращаем не то, что просили, а то, что CLI реально применил.
/// </summary>
/// <remarks>
/// Токены сверены с <c>config set-… --help</c>: CLI объявляет их СТРОЧНЫМИ
/// (<c>{socks,tun}</c>, <c>{auto,none,script}</c>) и верхний регистр отвергает.
/// </remarks>
public sealed class ConfigService(ICliRunner cli) : IConfigService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>Литерал сброса DNS-апстрима к серверам AdGuard (см. <c>set-dns --help</c>).</summary>
    private const string DnsDefault = "default";

    public async Task<VpnConfig> GetAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["config", "show"], Timeout, ct);
        if (!r.Ok)
            throw new ConfigCommandException(FirstNonEmpty(r.Stderr, r.Stdout, "config show завершился с ошибкой"));
        return ConfigParser.Parse(r.Stdout);
    }

    public Task<VpnConfig> SetModeAsync(VpnMode mode, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-mode", Token(mode)], ct);

    public Task<VpnConfig> SetSocksPortAsync(int port, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-socks-port", port.ToString()], ct);

    public Task<VpnConfig> SetSocksHostAsync(string host, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-socks-host", host], ct);

    public Task<VpnConfig> SetSocksUsernameAsync(string username, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-socks-username", username], ct);

    public Task<VpnConfig> SetSocksPasswordAsync(string password, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-socks-password", password], ct);

    public Task<VpnConfig> ClearSocksAuthAsync(CancellationToken ct = default) =>
        ApplyAsync(["config", "clear-socks-auth"], ct);

    public Task<VpnConfig> SetDnsAsync(string upstream, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-dns", upstream], ct);

    public Task<VpnConfig> ResetDnsAsync(CancellationToken ct = default) =>
        ApplyAsync(["config", "set-dns", DnsDefault], ct);

    public Task<VpnConfig> SetChangeSystemDnsAsync(bool on, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-change-system-dns", OnOff(on)], ct);

    public Task<VpnConfig> SetTunRoutingModeAsync(TunnelRoutingMode mode, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-tun-routing-mode", Token(mode)], ct);

    public Task<VpnConfig> SetProtocolAsync(VpnProtocol protocol, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-protocol", Token(protocol)], ct);

    public Task<VpnConfig> SetPostQuantumAsync(bool on, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-post-quantum", OnOff(on)], ct);

    public Task<VpnConfig> SetUpdateChannelAsync(UpdateChannel channel, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-update-channel", Token(channel)], ct);

    public Task<VpnConfig> SetShowNotificationsAsync(bool on, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-show-notifications", OnOff(on)], ct);

    public Task<VpnConfig> SetDebugLoggingAsync(bool on, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-debug-logging", OnOff(on)], ct);

    private async Task<VpnConfig> ApplyAsync(string[] args, CancellationToken ct)
    {
        var r = await cli.RunAsync(args, Timeout, ct);
        if (!r.Ok)
            throw new ConfigCommandException(
                FirstNonEmpty(r.Stderr, r.Stdout, $"{string.Join(' ', args)} завершился с ошибкой"));
        return await GetAsync(ct);
    }

    private static string OnOff(bool on) => on ? "on" : "off";

    // Имена членов перечислений совпадают с токенами CLI с точностью до регистра: Http2 → http2, None → none.
    private static string Token<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString()!.ToLowerInvariant();

    private static string FirstNonEmpty(params string[] xs) =>
        xs.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
}
