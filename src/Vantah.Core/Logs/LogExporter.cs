using Vantah.Core.Cli;
using Vantah.Core.Errors;

namespace Vantah.Core.Logs;

public sealed class LogExportException(AppError error) : Exception(error.ToString()), IAppErrorException
{
    public AppError Error { get; } = error;
}

/// <summary>Выгрузка логов через <c>export-logs -o &lt;path&gt; -f</c> (неинтерактивно).</summary>
public sealed class LogExporter(ICliRunner cli) : ILogExporter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task<string> ExportAsync(string outputPath, CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["export-logs", "-o", outputPath, "-f"], Timeout, ct);
        if (!r.Ok)
        {
            throw new LogExportException(AppError.FromCli(r, "export-logs"));
        }
        return outputPath;
    }
}
