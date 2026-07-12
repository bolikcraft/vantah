using System.Text.RegularExpressions;
using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Parsing;

public static class LicenseParser
{
    public static License Parse(string cliOutput)
    {
        var text = Ansi.Strip(cliOutput);
        var email  = Match(text, @"Logged in as (?<v>\S+)") ?? "";
        var plan   = Match(text, @"using the (?<v>\S+) version") ?? "UNKNOWN";
        var maxRaw = Match(text, @"Up to (?<v>\d+) devices");
        var maxDev = int.TryParse(maxRaw, out var d) ? d : 0;
        var renew  = Match(text, @"renewed on (?<v>\d{4}-\d{2}-\d{2})");
        return new License(email, plan, maxDev, renew);
    }

    private static string? Match(string s, string pattern)
    {
        var m = Regex.Match(s, pattern);
        return m.Success ? m.Groups["v"].Value : null;
    }
}
