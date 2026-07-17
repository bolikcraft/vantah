using Vantah.Core.Cli;
using Vantah.Core.Logs;
using Vantah.Core.Tests.Fakes;
using Xunit;

public class LogExporterTests
{
    [Fact]
    public async Task Runs_export_logs_with_output_and_force()
    {
        var cli = new FakeCliRunner().Enqueue("Logs exported");
        var exporter = new LogExporter(cli);

        var path = await exporter.ExportAsync("/tmp/out");

        Assert.Equal(new[] { "export-logs", "-o", "/tmp/out", "-f" }, cli.Calls[0]);
        Assert.Equal("/tmp/out", path);
    }

    [Fact]
    public async Task Failed_export_throws()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "cannot write"));
        var exporter = new LogExporter(cli);

        var ex = await Assert.ThrowsAsync<LogExportException>(() => exporter.ExportAsync("/tmp/out"));

        Assert.Contains("cannot write", ex.Message);
    }
}
