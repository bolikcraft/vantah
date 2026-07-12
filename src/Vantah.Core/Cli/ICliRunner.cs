namespace Vantah.Core.Cli;

public interface ICliRunner
{
    Task<CliResult> RunAsync(string[] args, TimeSpan? timeout = null, CancellationToken ct = default);
}
