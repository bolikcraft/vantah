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
            domains.Add(line);
        }

        // Единственная точка фильтрации: Normalize сам отбрасывает строки-«шум» CLI
        // (напр. «No exclusions configured», «Type a domain to add») своим предикатом
        // «похоже на домен» — дублировать его здесь незачем.
        return new ExclusionsSnapshot(mode, DomainNormalizer.Normalize(domains));
    }
}
