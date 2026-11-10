using System.Text.RegularExpressions;
using Vantah.Core.Cli;

namespace Vantah.Core.Update;

/// <summary>
/// Разбор вывода <c>adguardvpn-cli check-update</c>. Актуальная версия: «You are using the latest
/// version». Доступно обновление: «AdGuard VPN v1.8.2 is now available» — и в той же выдаче
/// встречается слово «latest» («update to the latest version»), поэтому признак обновления
/// проверяем раньше него. Прочий непустой вывод без слова «latest» считаем обновлением.
/// </summary>
public static partial class UpdateCheckParser
{
    public static UpdateStatus Parse(string cliOutput)
    {
        var text = Ansi.Strip(cliOutput ?? "").Trim();
        if (text.Length == 0)
            return new UpdateStatus(IsLatest: true, LatestVersion: null);

        var m = SemverRegex().Match(text);
        if (m.Success && text.Contains("now available", StringComparison.OrdinalIgnoreCase))
            return new UpdateStatus(IsLatest: false, LatestVersion: m.Value);

        if (text.Contains("latest", StringComparison.OrdinalIgnoreCase))
            return new UpdateStatus(IsLatest: true, LatestVersion: null);

        return new UpdateStatus(IsLatest: false, LatestVersion: m.Success ? m.Value : null);
    }

    [GeneratedRegex(@"\d+\.\d+\.\d+")]
    private static partial Regex SemverRegex();
}
