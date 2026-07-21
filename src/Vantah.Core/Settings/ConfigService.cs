using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Settings;

public sealed class ConfigCommandException(string message) : Exception(message);

/// <summary>
/// Настройки поверх <c>adguardvpn-cli config</c>. Пишем одной подкомандой <c>set-*</c>, затем
/// перечитываем <c>show</c>: возвращаем не то, что просили, а то, что CLI реально применил.
/// </summary>
/// <remarks>
/// Токены сверены с <c>config set-… --help</c>: CLI объявляет их строчными
/// (<c>{socks,tun}</c>, <c>{auto,none,script}</c>). На практике он регистронезависим и «TUN»
/// тоже принимает, но шлём объявленную форму — на неё он и рассчитан.
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

    /// <summary>
    /// Устанавливает SOCKS-логин. ВНИМАНИЕ: adguardvpn-cli принимает значение только позиционным
    /// аргументом, поэтому на время выполнения команды оно видно другим локальным пользователям
    /// через /proc/&lt;pid&gt;/cmdline и <c>ps</c>. Ограничение CLI.
    /// </summary>
    public Task<VpnConfig> SetSocksUsernameAsync(string username, CancellationToken ct = default) =>
        ApplySensitiveAsync(["config", "set-socks-username", username], "set-socks-username", ct);

    /// <summary>
    /// Устанавливает SOCKS-пароль. ВНИМАНИЕ: adguardvpn-cli принимает пароль только позиционным
    /// аргументом (флага stdin нет; интерактивный промпт требует TTY, обычный pipe не подходит —
    /// нужен был бы PTY). Поэтому на время выполнения команды пароль виден другим локальным
    /// пользователям через /proc/&lt;pid&gt;/cmdline и <c>ps</c>. Ограничение CLI; безопасная передача
    /// потребовала бы PTY-реализации (вне текущего объёма).
    /// </summary>
    public Task<VpnConfig> SetSocksPasswordAsync(string password, CancellationToken ct = default) =>
        ApplySensitiveAsync(["config", "set-socks-password", password], "set-socks-password", ct);

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

    public Task<VpnConfig> SetCrashReportingAsync(bool on, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-crash-reporting", OnOff(on)], ct);

    public Task<VpnConfig> SetTelemetryAsync(bool on, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-telemetry", OnOff(on)], ct);

    public Task<VpnConfig> SetShowHintsAsync(bool on, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-show-hints", OnOff(on)], ct);

    public Task<VpnConfig> SetBoundIfOverrideAsync(string iface, CancellationToken ct = default) =>
        ApplyAsync(["config", "set-bound-if-override", iface], ct);

    public async Task<string> CreateRouteScriptAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["config", "create-route-script"], Timeout, ct);
        if (!r.Ok)
            throw new ConfigCommandException(
                FirstNonEmpty(r.Stderr, r.Stdout, "create-route-script завершился с ошибкой"));
        return FirstNonEmpty(r.Stdout, r.Stderr, "");
    }

    private async Task<VpnConfig> ApplyAsync(string[] args, CancellationToken ct)
    {
        var r = await cli.RunAsync(args, Timeout, ct);
        if (!r.Ok)
            throw new ConfigCommandException(
                FirstNonEmpty(r.Stderr, r.Stdout, $"{string.Join(' ', args)} завершился с ошибкой"));
        return await GetAsync(ct);
    }

    /// <summary>
    /// Как <see cref="ApplyAsync"/>, но для команд с чувствительными аргументами (пароль):
    /// в fallback-сообщении об ошибке и в сообщении о таймауте используется <paramref name="safeLabel"/>,
    /// а не сами <paramref name="args"/> — иначе, например, пароль из set-socks-password попал бы
    /// в текст <see cref="ConfigCommandException"/> (fallback) либо <see cref="TimeoutException"/>
    /// (её бросает <c>CliRunner</c>, включая args в сообщение), а оттуда — в UI/логи.
    /// </summary>
    private async Task<VpnConfig> ApplySensitiveAsync(string[] args, string safeLabel, CancellationToken ct)
    {
        CliResult r;
        try
        {
            r = await cli.RunAsync(args, Timeout, ct);
        }
        catch (TimeoutException)
        {
            throw new ConfigCommandException($"{safeLabel} превысил таймаут");
        }
        if (!r.Ok)
            throw new ConfigCommandException(FirstNonEmpty(r.Stderr, r.Stdout, $"{safeLabel} завершился с ошибкой"));
        return await GetAsync(ct);
    }

    private static string OnOff(bool on) => on ? "on" : "off";

    // Имена членов перечислений совпадают с токенами CLI с точностью до регистра: Http2 → http2, None → none.
    private static string Token<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString()!.ToLowerInvariant();

    private static string FirstNonEmpty(params string[] xs) =>
        xs.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
}
