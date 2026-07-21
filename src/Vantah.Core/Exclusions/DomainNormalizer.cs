using System.Net;
using System.Net.Sockets;

namespace Vantah.Core.Exclusions;

public static class DomainNormalizer
{
    /// <summary>Trim, отбросить пустые и не-домены, dedupe без учёта регистра (регистр первого вхождения сохраняется).</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> domains)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var d in domains)
        {
            var trimmed = d.Trim();
            if (!IsAcceptableDomain(trimmed)) continue;
            if (seen.Add(trimmed)) result.Add(trimmed);
        }
        return result;
    }

    // Отбрасываем всё, что не похоже на домен: строка, начинающаяся с «-», будет разобрана
    // CLI как опция, а не как позиционный аргумент (option injection при импорте чужого файла).
    // Это ключевая защита, она безусловна. Требование точки — фильтр шумовых строк вывода CLI
    // (напр. «No exclusions configured»); оно ослаблено ровно на один случай — строка является
    // IPv6-литералом, который adguardvpn-cli принимает как исключение (site-exclusions add
    // --help: `IPv6Address`, `[IPv6Address]`, `[IPv6Address]:port`, `IPv6Address/mask`).
    private static bool IsAcceptableDomain(string s) =>
        s.Length > 0 && s.Length <= 253 &&
        !s.StartsWith('-') &&
        !s.Contains(' ') &&
        (s.Contains('.') || LooksLikeIpv6(s)) &&
        s.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '*' or ':' or '/' or '[' or ']');

    // Распознаём IPv6 через IPAddress.TryParse, а не самодельной эвристикой по двоеточиям:
    // эвристика «есть ':' — значит IPv6» пропустила бы мусор вроде «::::» и «a:b», а грамматика
    // IPv6 (сжатие «::», встроенный IPv4-хвост, число групп) слишком нетривиальна, чтобы
    // повторять её вручную. TryParse не понимает форму в скобках с портом и CIDR-маску,
    // поэтому их снимаем сами и проверяем отдельно.
    private static bool LooksLikeIpv6(string s)
    {
        var slash = s.IndexOf('/');
        if (slash >= 0)
        {
            if (!byte.TryParse(s[(slash + 1)..], out var prefix) || prefix > 128) return false;
            s = s[..slash];
        }

        if (s.StartsWith('['))
        {
            var close = s.IndexOf(']');
            if (close < 0) return false;
            var rest = s[(close + 1)..];
            if (rest.Length > 0 && (rest[0] != ':' || !ushort.TryParse(rest[1..], out _))) return false;
            s = s[1..close];
        }

        return IPAddress.TryParse(s, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6;
    }

    /// <summary>URL/токен из буфера → доменное имя (или null, если не домен). Порт adgui parseDomainFromClipboard.</summary>
    public static string? ParseUrlToDomain(string content)
    {
        content = content.Trim();
        if (content.Length == 0) return null;

        var token = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } f
            ? f[0] : null;
        if (token is null) return null;

        if (!token.Contains("://")) token = "http://" + token;
        if (!Uri.TryCreate(token, UriKind.Absolute, out var uri)) return null;

        var host = uri.Host.ToLowerInvariant().TrimEnd('.');
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
        if (!host.Contains('.')) return null;
        return host;
    }

    /// <summary>Из домена делает пару записей-исключений: www + wildcard (как adgui при вставке).</summary>
    public static IReadOnlyList<string> PasteEntries(string domain) =>
        new[] { $"www.{domain}", $"*.{domain}" };
}
