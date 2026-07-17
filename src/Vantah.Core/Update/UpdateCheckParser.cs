using System.Text.RegularExpressions;
using Vantah.Core.Cli;

namespace Vantah.Core.Update;

/// <summary>
/// Разбор вывода <c>adguardvpn-cli check-update</c>. Наблюдался только текст «latest version»;
/// формулировка обновления неизвестна, поэтому решаем консервативно: нет слова «latest» и вывод
/// непустой → считаем, что обновление доступно.
/// </summary>
public static partial class UpdateCheckParser
{
    public static UpdateStatus Parse(string cliOutput)
    {
        var text = Ansi.Strip(cliOutput ?? "").Trim();
        if (text.Length == 0)
            return new UpdateStatus(IsLatest: true, LatestVersion: null);

        if (text.Contains("latest", StringComparison.OrdinalIgnoreCase))
            return new UpdateStatus(IsLatest: true, LatestVersion: null);

        var m = SemverRegex().Match(text);
        return new UpdateStatus(IsLatest: false, LatestVersion: m.Success ? m.Value : null);
    }

    [GeneratedRegex(@"\d+\.\d+\.\d+")]
    private static partial Regex SemverRegex();
}
