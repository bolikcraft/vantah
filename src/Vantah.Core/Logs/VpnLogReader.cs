using System.Text;
using Vantah.Core.Config;

namespace Vantah.Core.Logs;

/// <summary>
/// Читает хвост лога adguardvpn-cli (<c>~/.local/share/adguardvpn-cli/app.log</c>).
/// Отдаёт последние строки новыми сверху и отфильтровывает шум от нашего же опроса статуса.
/// </summary>
public sealed class VpnLogReader
{
    private const int MaxBytes = 128 * 1024;   // читаем только хвост файла
    private readonly string _path;

    public VpnLogReader(string? path = null) => _path = path ?? DefaultPath();

    public static string DefaultPath() =>
        Path.Combine(VantahPaths.DataHome, "adguardvpn-cli", "app.log");

    /// <summary>Последние значимые строки лога, новые первыми (шумные строки опроса выброшены).</summary>
    public IReadOnlyList<string> ReadTail(int maxLines = 300)
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<string>();

            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var start = Math.Max(0, fs.Length - MaxBytes);
            fs.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var lines = reader.ReadToEnd().Split('\n');

            var result = new List<string>();
            // Если обрезали с середины файла — первая строка обрывочная, пропускаем её.
            for (int i = start > 0 ? 1 : 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (line.Length == 0 || IsNoise(line)) continue;
                result.Add(line);
            }

            if (result.Count > maxLines)
                result = result.GetRange(result.Count - maxLines, maxLines);
            result.Reverse();   // новые сверху
            return result;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    // Строки, которые генерирует наш собственный периодический `status` — не относятся к работе VPN.
    private static bool IsNoise(string line) =>
        line.Contains("Start CLI App") ||
        line.Contains("get_default_interface");
}
