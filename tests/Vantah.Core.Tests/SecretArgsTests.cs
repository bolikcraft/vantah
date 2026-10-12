using Vantah.Core.Cli;
using Xunit;

/// <summary>
/// Одна маска для лога, текста таймаута и окна процессов. Логин и пароль SOCKS и адрес DNS
/// CLI принимает позиционным аргументом, а в частном адресе AdGuard DNS есть ID клиента.
/// </summary>
public class SecretArgsTests
{
    private const string Exe = "/opt/adguardvpn_cli/adguardvpn-cli";

    [Theory]
    [InlineData(new[] { "config", "set-socks-password", "s3cr3t" }, "config set-socks-password ***")]
    [InlineData(new[] { "config", "set-socks-username", "alice" }, "config set-socks-username ***")]
    [InlineData(new[] { "config", "set-socks-password", "two words" }, "config set-socks-password ***")]
    [InlineData(new[] { "config", "set-dns", "https://d.adguard-dns.com/dns-query/abc123" }, "config set-dns ***")]
    [InlineData(new[] { "config", "set-dns", "tls://abc123.d.adguard-dns.com" }, "config set-dns ***")]
    [InlineData(new[] { Exe, "config", "set-socks-password", "s3cr3t" }, Exe + " config set-socks-password ***")]
    [InlineData(new[] { "config", "set-dns" }, "config set-dns")]
    [InlineData(new[] { "config", "set-socks-host", "127.0.0.1" }, "config set-socks-host 127.0.0.1")]
    [InlineData(new[] { "list-locations", "300" }, "list-locations 300")]
    [InlineData(new[] { "status" }, "status")]
    public void Value_after_a_secret_command_is_masked(string[] args, string expected)
    {
        Assert.Equal(expected, SecretArgs.Join(args));
    }
}
