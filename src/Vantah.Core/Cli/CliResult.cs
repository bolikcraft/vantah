namespace Vantah.Core.Cli;

public sealed record CliResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Ok => ExitCode == 0;
}
