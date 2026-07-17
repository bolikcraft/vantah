using Vantah.Core.Cli;

namespace Vantah.Core.Logs;

public sealed class LogExportException(string message) : Exception(message);

/// <summary>Выгрузка логов через <c>export-logs -o &lt;path&gt; -f</c> (неинтерактивно).</summary>
public sealed class LogExporter(ICliRunner cli) : ILogExporter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task<string> ExportAsync(string outputPath, CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["export-logs", "-o", outputPath, "-f"], Timeout, ct);
        if (!r.Ok)
        {
            var msg = new[] { r.Stderr, r.Stdout }
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim()
                ?? "export-logs завершился с ошибкой";
            throw new LogExportException(msg);
        }
        return outputPath;
    }
}
