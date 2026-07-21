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
    // Требование точки — главный фильтр шумовых строк, поэтому оно не ослабляется.
    private static bool IsAcceptableDomain(string s) =>
        s.Length > 0 && s.Length <= 253 &&
        !s.StartsWith('-') &&
        !s.Contains(' ') &&
        s.Contains('.') &&
        s.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '*' or ':' or '/');

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
