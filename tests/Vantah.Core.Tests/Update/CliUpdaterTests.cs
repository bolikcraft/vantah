using Vantah.Core.Cli;
using Vantah.Core.Tests.Fakes;
using Vantah.Core.Update;
using Xunit;

namespace Vantah.Core.Tests.Update;

public class CliUpdaterTests
{
    [Fact]
    public async Task Exit_zero_means_updated()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(0, "Update completed", ""));
        var r = await new CliUpdater(cli).UpdateAsync();
        Assert.Equal(UpdateOutcome.Updated, r.Outcome);
    }

    [Fact]
    public async Task Exit_17_means_already_latest()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(17, "You are using the latest version", ""));
        var r = await new CliUpdater(cli).UpdateAsync();
        Assert.Equal(UpdateOutcome.AlreadyLatest, r.Outcome);
    }

    [Fact]
    public async Task Other_nonzero_exit_means_failed_and_keeps_output()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "boom"));
        var r = await new CliUpdater(cli).UpdateAsync();
        Assert.Equal(UpdateOutcome.Failed, r.Outcome);
        Assert.Contains("boom", r.Output);
    }
}
