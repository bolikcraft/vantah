using Vantah.Core.Cli;
using Vantah.Core.Tests.Fakes;
using Vantah.Core.Update;
using Xunit;

public class UpdateCheckerTests
{
    [Fact]
    public async Task Runs_check_update_and_parses()
    {
        var cli = new FakeCliRunner().Enqueue("You are using the latest version");
        var checker = new UpdateChecker(cli);

        var s = await checker.CheckAsync();

        Assert.Equal(new[] { "check-update" }, cli.Calls[0]);
        Assert.True(s.IsLatest);
    }

    [Fact]
    public async Task Failed_check_is_treated_as_latest()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "network error"));
        var checker = new UpdateChecker(cli);

        var s = await checker.CheckAsync();

        Assert.True(s.IsLatest);
    }
}
