namespace Vantah.Core.Logs;

public interface ILogExporter
{
    /// <summary>Выгружает логи в zip в указанную папку/путь; возвращает переданный путь.</summary>
    Task<string> ExportAsync(string outputPath, CancellationToken ct = default);
}
