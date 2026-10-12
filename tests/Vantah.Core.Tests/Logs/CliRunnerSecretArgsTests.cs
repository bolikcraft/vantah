using Vantah.Core.Cli;
using Vantah.Core.Tests.Fakes;
using Xunit;

/// <summary>
/// Команды настройки не входят в опрос и пишутся в лог всегда. Логин и пароль SOCKS и адрес DNS
/// CLI принимает позиционным аргументом, поэтому без маски они попадали бы в app.log открытым текстом.
/// </summary>
public class CliRunnerSecretArgsTests
{
    private static string Bin(string name) =>
        new[] { "/bin/" + name, "/usr/bin/" + name }.First(File.Exists);

    [Fact]
    public async Task Password_does_not_reach_the_log()
    {
        var log = new FakeAppLog();
        var runner = new CliRunner(Bin("true"), log);

        await runner.RunAsync(["config", "set-socks-password", "s3cr3t"]);

        var line = Assert.Single(log.Lines);
        Assert.Matches(@"^cli: config set-socks-password \*\*\* → rc=0, \d+ ms$", line);
        Assert.DoesNotContain("s3cr3t", line);
    }

    [Fact]
    public async Task Dns_address_does_not_reach_the_log()
    {
        var log = new FakeAppLog();
        var runner = new CliRunner(Bin("true"), log);

        await runner.RunAsync(["config", "set-dns", "https://d.adguard-dns.com/dns-query/abc123"]);

        var line = Assert.Single(log.Lines);
        Assert.Matches(@"^cli: config set-dns \*\*\* → rc=0, \d+ ms$", line);
        Assert.DoesNotContain("abc123", line);
    }

    [Fact]
    public async Task Timeout_hides_the_password_in_the_log_and_in_the_error()
    {
        var log = new FakeAppLog();
        // `sh -c "sleep 30"` висит до таймаута, а остальные аргументы шелл получает как $0, $1, ….
        var runner = new CliRunner(Bin("sh"), log);

        var ex = await Assert.ThrowsAsync<CliTimeoutException>(() => runner.RunAsync(
            ["-c", "sleep 30", "config", "set-socks-password", "s3cr3t"], TimeSpan.FromMilliseconds(150)));

        Assert.EndsWith("config set-socks-password ***", ex.Error.Argument);
        Assert.DoesNotContain("s3cr3t", ex.Message);
        Assert.DoesNotContain("s3cr3t", Assert.Single(log.Lines));
    }
}
