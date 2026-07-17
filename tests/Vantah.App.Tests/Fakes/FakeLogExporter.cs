using Vantah.Core.Logs;

namespace Vantah.App.Tests.Fakes;

public sealed class FakeLogExporter : ILogExporter
{
    public List<string> Exported { get; } = [];
    public Task<string> ExportAsync(string outputPath, CancellationToken ct = default)
    {
        Exported.Add(outputPath);
        return Task.FromResult(outputPath);
    }
}
