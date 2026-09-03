using Vantah.Core.Cli;
using Vantah.Core.Tests.Fakes;
using Xunit;

/// <summary>
/// Лог вызовов CLI. Раннер запускает настоящий процесс, поэтому берём системные утилиты:
/// true — быстрый успех, false — ненулевой код, sleep — таймаут.
/// </summary>
public class CliRunnerLogTests
{
    private static string Bin(string name)
    {
        foreach (var dir in new[] { "/bin", "/usr/bin" })
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException($"нет утилиты {name}");
    }

    [Fact]
    public async Task Fast_successful_status_is_not_logged()
    {
        var log = new FakeAppLog();

        await new CliRunner(Bin("true"), log).RunAsync(["status"]);

        Assert.Empty(log.Lines);
    }

    [Fact]
    public async Task Failed_status_is_logged_with_exit_code_and_duration()
    {
        var log = new FakeAppLog();

        await new CliRunner(Bin("false"), log).RunAsync(["status"]);

        Assert.Matches(@"^cli: status → rc=1, \d+ ms$", Assert.Single(log.Lines));
    }

    [Fact]
    public async Task Fast_successful_license_is_not_logged()
    {
        var log = new FakeAppLog();

        await new CliRunner(Bin("true"), log).RunAsync(["license"]);

        Assert.Empty(log.Lines);
    }

    [Fact]
    public async Task Command_other_than_polling_is_logged_even_when_fast_and_successful()
    {
        var log = new FakeAppLog();

        await new CliRunner(Bin("true"), log).RunAsync(["disconnect"]);

        Assert.Matches(@"^cli: disconnect → rc=0, \d+ ms$", Assert.Single(log.Lines));
    }

    [Fact]
    public async Task Timeout_is_logged_separately()
    {
        var log = new FakeAppLog();
        var runner = new CliRunner(Bin("sleep"), log);

        await Assert.ThrowsAsync<CliTimeoutException>(() =>
            runner.RunAsync(["30"], TimeSpan.FromMilliseconds(150)));

        Assert.Matches(@"^cli: 30 → таймаут через \d+ ms$", Assert.Single(log.Lines));
    }

    [Fact]
    public async Task Disabled_log_stays_empty()
    {
        var log = new FakeAppLog { Enabled = false };

        await new CliRunner(Bin("false"), log).RunAsync(["license"]);

        Assert.Empty(log.Lines);
    }

    // Порог «долгого» вызова проверяем на самом правиле: ждать в тесте больше двух секунд незачем.
    [Theory]
    [InlineData(new[] { "status" }, 0, 118, false)]
    [InlineData(new[] { "status" }, 0, 2001, true)]
    [InlineData(new[] { "status" }, 1, 5, true)]
    [InlineData(new[] { "license" }, 0, 5, false)]
    [InlineData(new[] { "license" }, 17, 5, true)]
    [InlineData(new[] { "disconnect" }, 0, 5, true)]
    [InlineData(new[] { "list-locations", "300" }, 0, 5, true)]
    public void Slow_or_failed_or_non_polling_calls_are_worth_logging(
        string[] args, int exitCode, long elapsedMs, bool expected)
    {
        Assert.Equal(expected, CliRunner.ShouldLogCall(args, exitCode, elapsedMs));
    }
}
