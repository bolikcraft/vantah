using Vantah.Core.Config;
using Vantah.Core.Models;

namespace Vantah.Core.Exclusions;

public sealed class ExclusionsStore
{
    private readonly string _dir;

    public ExclusionsStore(string? dir = null)
    {
        _dir = dir ?? Path.Combine(VantahPaths.ConfigDir, "site-exclusions");
    }

    public string FilePath(SiteExclusionMode mode) =>
        Path.Combine(_dir, mode == SiteExclusionMode.Selective ? "selective.txt" : "general.txt");

    public IReadOnlyList<string> Load(SiteExclusionMode mode)
    {
        // Устойчивость: заблокированный/битый per-mode файл не должен ронять путь перезагрузки.
        try
        {
            var path = FilePath(mode);
            return File.Exists(path) ? Import(path) : Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }

    public void Save(SiteExclusionMode mode, IEnumerable<string> domains)
    {
        Directory.CreateDirectory(_dir);
        WriteAtomic(FilePath(mode), DomainNormalizer.Normalize(domains));
    }

    /// <summary>Экспорт в произвольный файл (.vantah/.txt) — newline, нормализовано.</summary>
    public void Export(string path, IEnumerable<string> domains)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        WriteAtomic(path, DomainNormalizer.Normalize(domains));
    }

    /// <summary>Импорт из newline-файла — нормализовано.</summary>
    public IReadOnlyList<string> Import(string path) =>
        DomainNormalizer.Normalize(File.ReadAllLines(path));

    private static void WriteAtomic(string path, IReadOnlyList<string> normalized)
    {
        var dir = Path.GetDirectoryName(path)!;
        var content = normalized.Count == 0 ? "" : string.Join('\n', normalized) + "\n";
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
