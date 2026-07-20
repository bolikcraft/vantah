using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Exclusions;

public static class ExclusionsParser
{
    public static ExclusionsSnapshot Parse(string cliOutput)
    {
        var mode = SiteExclusionMode.General;
        var domains = new List<string>();

        foreach (var rawLine in Ansi.Strip(cliOutput).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (line.Contains("exclusions for", StringComparison.OrdinalIgnoreCase))
            {
                mode = line.Contains("selective", StringComparison.OrdinalIgnoreCase)
                    ? SiteExclusionMode.Selective
                    : SiteExclusionMode.General;
                continue;
            }
            if (LooksLikeDomain(line))
                domains.Add(line);
        }

        return new ExclusionsSnapshot(mode, DomainNormalizer.Normalize(domains));
    }

    // Отсеивает строки-«шум» CLI (напр. «No exclusions configured», «Type a domain to add»),
    // которые не являются доменом/wildcard-доменом/IP-исключением. Требование точки — главный
    // фильтр шумовых строк, поэтому оно не ослабляется: чистые IPv6-литералы без точки
    // (напр. `2001:db8::1`) под предикат не подходят и не распознаются как исключение — это
    // осознанный компромисс, т.к. site-исключения adguardvpn-cli — это домены/IPv4/CIDR.
    private static bool LooksLikeDomain(string s) =>
        s.Length <= 253 && !s.Contains(' ') && s.Contains('.') &&
        s.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '*' or ':' or '/');
}
