using System.Text.RegularExpressions;
using Vantah.Core.Cli;

namespace Vantah.Core.Auth;

/// <summary>
/// Достаёт из вывода `adguardvpn-cli login` приглашение device-code: ссылку авторизации,
/// код (query-параметр <c>user_code</c>) и время жизни ссылки. Реальная строка (CLI v1.7.13):
/// «You need to authorize in your browser. The following link will be available for N seconds: URL».
/// </summary>
public static partial class DeviceCodeParser
{
    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"user_code=([A-Za-z0-9\-]+)")]
    private static partial Regex UserCodeRegex();

    [GeneratedRegex(@"available for\s+(\d+)\s+seconds")]
    private static partial Regex SecondsRegex();

    /// <summary>Возвращает приглашение, если в тексте есть ссылка авторизации; иначе null.</summary>
    public static DeviceCodePrompt? Parse(string cliOutput)
    {
        if (string.IsNullOrWhiteSpace(cliOutput)) return null;
        var text = Ansi.Strip(cliOutput);

        var url = UrlRegex().Match(text);
        if (!url.Success) return null;

        // Хвостовую пунктуацию/кавычки к ссылке не приклеиваем.
        var link = url.Value.TrimEnd('.', ',', ')', '"', '\'');

        string? code = UserCodeRegex().Match(text) is { Success: true } c ? c.Groups[1].Value : null;
        int? seconds = SecondsRegex().Match(text) is { Success: true } s && int.TryParse(s.Groups[1].Value, out var n)
            ? n : null;

        return new DeviceCodePrompt(link, code, seconds);
    }
}
