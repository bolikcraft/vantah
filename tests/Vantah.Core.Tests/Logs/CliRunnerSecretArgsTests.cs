using Vantah.Core.Cli;
using Vantah.Core.Tests.Fakes;
using Xunit;

/// <summary>
/// Команды настройки не входят в опрос и пишутся в лог всегда. Логин и пароль SOCKS CLI
/// принимает позиционным аргументом, поэтому без маски они попадали бы в app.log открытым текстом.
/// </summary>
public class CliRunnerSecretArgsTests
{
    [Theory]
    [InlineData(new[] { "config", "set-socks-password", "s3cr3t" }, "config set-socks-password ***")]
    [InlineData(new[] { "config", "set-socks-username", "alice" }, "config set-socks-username ***")]
    [InlineData(new[] { "config", "set-socks-host", "127.0.0.1" }, "config set-socks-host 127.0.0.1")]
    [InlineData(new[] { "list-locations", "300" }, "list-locations 300")]
    [InlineData(new[] { "status" }, "status")]
    public void Secret_values_are_masked(string[] args, string expected)
    {
        Assert.Equal(expected, CliRunner.ArgsForLog(args));
    }

    [Fact]
    public async Task Password_does_not_reach_the_log()
    {
        var log = new FakeAppLog();
        var runner = new CliRunner(new[] { "/bin/true", "/usr/bin/true" }.First(File.Exists), log);

        await runner.RunAsync(["config", "set-socks-password", "s3cr3t"]);

        var line = Assert.Single(log.Lines);
        Assert.Matches(@"^cli: config set-socks-password \*\*\* → rc=0, \d+ ms$", line);
        Assert.DoesNotContain("s3cr3t", line);
    }
}
