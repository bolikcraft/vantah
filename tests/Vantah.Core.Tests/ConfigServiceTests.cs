using Vantah.Core.Cli;
using Vantah.Core.Models;
using Vantah.Core.Settings;
using Vantah.Core.Tests.Fakes;
using Xunit;

public class ConfigServiceTests
{
    private const string Show =
        "\tMode: tun\n\tSOCKS port: 1080\n\tProtocol: Default (auto)\n\tUpdate channel: RELEASE\n";

    [Fact]
    public async Task GetAsync_reads_show_and_parses()
    {
        var cli = new FakeCliRunner().Enqueue(Show);
        var svc = new ConfigService(cli);

        var cfg = await svc.GetAsync();

        Assert.Equal(new[] { "config", "show" }, cli.Calls[0]);
        Assert.Equal(VpnMode.Tun, cfg.Mode);
        Assert.Equal(1080, cfg.SocksPort);
    }

    [Fact]
    public async Task SetProtocol_calls_set_protocol_then_rereads_show()
    {
        var cli = new FakeCliRunner()
            .Enqueue("")     // config set-protocol quic
            .Enqueue(Show);  // config show — подтверждаем результат перечитыванием
        var svc = new ConfigService(cli);

        var cfg = await svc.SetProtocolAsync(VpnProtocol.Quic);

        Assert.Equal(new[] { "config", "set-protocol", "quic" }, cli.Calls[0]);
        Assert.Equal(new[] { "config", "show" }, cli.Calls[1]);
        Assert.NotNull(cfg);
    }

    // CLI объявляет `set-mode mode TEXT:{socks,tun}` — шлём объявленную, строчную форму.
    [Fact]
    public async Task SetMode_passes_lowercase_token()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.SetModeAsync(VpnMode.Socks);

        Assert.Equal(new[] { "config", "set-mode", "socks" }, cli.Calls[0]);
    }

    // `set-tun-routing-mode mode TEXT:{auto,none,script}` — тоже объявлен строчным.
    [Fact]
    public async Task SetTunRoutingMode_passes_lowercase_token()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.SetTunRoutingModeAsync(TunnelRoutingMode.None);

        Assert.Equal(new[] { "config", "set-tun-routing-mode", "none" }, cli.Calls[0]);
    }

    [Fact]
    public async Task Toggle_setters_pass_on_off()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show).Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.SetPostQuantumAsync(false);
        await svc.SetChangeSystemDnsAsync(true);

        Assert.Equal(new[] { "config", "set-post-quantum", "off" }, cli.Calls[0]);
        Assert.Equal(new[] { "config", "set-change-system-dns", "on" }, cli.Calls[2]);
    }

    [Fact]
    public async Task SetSocksPort_and_channel_map_correctly()
    {
        var cli = new FakeCliRunner()
            .Enqueue("").Enqueue(Show)   // set-socks-port
            .Enqueue("").Enqueue(Show);  // set-update-channel
        var svc = new ConfigService(cli);

        await svc.SetSocksPortAsync(8899);
        await svc.SetUpdateChannelAsync(UpdateChannel.Nightly);

        Assert.Equal(new[] { "config", "set-socks-port", "8899" }, cli.Calls[0]);
        Assert.Equal(new[] { "config", "set-update-channel", "nightly" }, cli.Calls[2]);
    }

    [Fact]
    public async Task ClearSocksAuth_has_no_extra_args()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.ClearSocksAuthAsync();

        Assert.Equal(new[] { "config", "clear-socks-auth" }, cli.Calls[0]);
    }

    // CLI принимает литерал «default» — так DNS-апстрим возвращается к серверам AdGuard.
    [Fact]
    public async Task ResetDns_passes_default_literal()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.ResetDnsAsync();

        Assert.Equal(new[] { "config", "set-dns", "default" }, cli.Calls[0]);
    }

    [Fact]
    public async Task SetDns_passes_upstream()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.SetDnsAsync("94.140.14.14");

        Assert.Equal(new[] { "config", "set-dns", "94.140.14.14" }, cli.Calls[0]);
    }

    [Fact]
    public async Task SetCrashReporting_passes_on_off()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.SetCrashReportingAsync(true);

        Assert.Equal(new[] { "config", "set-crash-reporting", "on" }, cli.Calls[0]);
    }

    [Fact]
    public async Task SetTelemetry_passes_on_off()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.SetTelemetryAsync(false);

        Assert.Equal(new[] { "config", "set-telemetry", "off" }, cli.Calls[0]);
    }

    [Fact]
    public async Task SetShowHints_passes_on_off()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.SetShowHintsAsync(true);

        Assert.Equal(new[] { "config", "set-show-hints", "on" }, cli.Calls[0]);
    }

    [Fact]
    public async Task Failed_set_throws_with_stderr()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "invalid port"));
        var svc = new ConfigService(cli);

        var ex = await Assert.ThrowsAsync<ConfigCommandException>(() => svc.SetSocksPortAsync(-1));

        Assert.Contains("invalid port", ex.Message);
    }

    // Неуспешный `show` — не молчаливый пустой конфиг: E3 уже ловил, что забытая проверка r.Ok
    // выдаёт ошибку CLI за валидные данные.
    [Fact]
    public async Task Failed_show_throws()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "not logged in"));
        var svc = new ConfigService(cli);

        var ex = await Assert.ThrowsAsync<ConfigCommandException>(() => svc.GetAsync());

        Assert.Contains("not logged in", ex.Message);
    }

    [Fact]
    public async Task SetBoundIfOverride_passes_interface()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.SetBoundIfOverrideAsync("eth0");

        Assert.Equal(new[] { "config", "set-bound-if-override", "eth0" }, cli.Calls[0]);
    }

    // Пустая строка = отключить override; CLI объявляет пустой аргумент явно.
    [Fact]
    public async Task SetBoundIfOverride_empty_disables()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue(Show);
        var svc = new ConfigService(cli);

        await svc.SetBoundIfOverrideAsync("");

        Assert.Equal(new[] { "config", "set-bound-if-override", "" }, cli.Calls[0]);
    }

    [Fact]
    public async Task CreateRouteScript_returns_cli_output()
    {
        var cli = new FakeCliRunner().Enqueue("Route script created at /etc/adguardvpn/route.sh");
        var svc = new ConfigService(cli);

        var output = await svc.CreateRouteScriptAsync();

        Assert.Equal(new[] { "config", "create-route-script" }, cli.Calls[0]);
        Assert.Contains("route.sh", output);
    }

    [Fact]
    public async Task CreateRouteScript_throws_on_failure()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "permission denied"));
        var svc = new ConfigService(cli);

        var ex = await Assert.ThrowsAsync<ConfigCommandException>(() => svc.CreateRouteScriptAsync());

        Assert.Contains("permission denied", ex.Message);
    }

    // Пароль не должен попадать в текст исключения — ни через stderr (тут его и так нет),
    // ни через fallback-сообщение об ошибке.
    [Fact]
    public async Task Socks_password_is_not_leaked_in_error_message()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "internal error"));
        var svc = new ConfigService(cli);

        var ex = await Assert.ThrowsAsync<ConfigCommandException>(() => svc.SetSocksPasswordAsync("S3cr3t!"));

        Assert.DoesNotContain("S3cr3t!", ex.Message);
    }

    // И stderr, и stdout пустые — срабатывает fallback-сообщение. Оно должно содержать
    // безопасную метку команды, а не реальные args (в которых пароль).
    [Fact]
    public async Task Socks_password_fallback_message_uses_safe_label()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", ""));
        var svc = new ConfigService(cli);

        var ex = await Assert.ThrowsAsync<ConfigCommandException>(() => svc.SetSocksPasswordAsync("S3cr3t!"));

        Assert.DoesNotContain("S3cr3t!", ex.Message);
        Assert.Contains("set-socks-password", ex.Message);
    }

    // CliRunner при таймауте бросает TimeoutException, включающую args (и пароль) в текст —
    // ApplySensitiveAsync должен перехватывать это и переизлагать безопасным сообщением.
    [Fact]
    public async Task Socks_password_is_not_leaked_on_timeout()
    {
        var cli = new ThrowingTimeoutRunner();
        var svc = new ConfigService(cli);

        var ex = await Assert.ThrowsAsync<ConfigCommandException>(() => svc.SetSocksPasswordAsync("S3cr3t!"));

        Assert.DoesNotContain("S3cr3t!", ex.Message);
        Assert.Contains("set-socks-password", ex.Message);
    }

    [Fact]
    public async Task Socks_username_is_not_leaked_in_error_message()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "internal error"));
        var svc = new ConfigService(cli);

        var ex = await Assert.ThrowsAsync<ConfigCommandException>(() => svc.SetSocksUsernameAsync("alice-secret"));

        Assert.DoesNotContain("alice-secret", ex.Message);
    }

    // И stderr, и stdout пустые — срабатывает fallback-сообщение. Оно должно содержать
    // безопасную метку команды, а не реальные args (в которых логин).
    [Fact]
    public async Task Socks_username_fallback_message_uses_safe_label()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", ""));
        var svc = new ConfigService(cli);

        var ex = await Assert.ThrowsAsync<ConfigCommandException>(() => svc.SetSocksUsernameAsync("alice-secret"));

        Assert.DoesNotContain("alice-secret", ex.Message);
        Assert.Contains("set-socks-username", ex.Message);
    }

    // CliRunner при таймауте бросает TimeoutException, включающую args (и логин) в текст —
    // ApplySensitiveAsync должен перехватывать это и переизлагать безопасным сообщением.
    [Fact]
    public async Task Socks_username_is_not_leaked_on_timeout()
    {
        var cli = new ThrowingTimeoutRunner();
        var svc = new ConfigService(cli);

        var ex = await Assert.ThrowsAsync<ConfigCommandException>(() => svc.SetSocksUsernameAsync("alice-secret"));

        Assert.DoesNotContain("alice-secret", ex.Message);
        Assert.Contains("set-socks-username", ex.Message);
    }

    private sealed class ThrowingTimeoutRunner : ICliRunner
    {
        public Task<CliResult> RunAsync(string[] args, TimeSpan? timeout = null, CancellationToken ct = default) =>
            throw new TimeoutException(
                $"adguardvpn-cli {string.Join(' ', args)} превысил таймаут");
    }
}
