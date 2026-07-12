using System.Text.RegularExpressions;
using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Parsing;

public static partial class LocationsParser
{
    [GeneratedRegex(@"^(?<iso>[A-Z]{2})\s{2,}(?<country>.+?)\s{2,}(?<city>.+?)\s{2,}(?<ping>\d+)\s*$")]
    private static partial Regex RowRegex();

    public static IReadOnlyList<Location> Parse(string cliOutput)
    {
        var result = new List<Location>();
        foreach (var line in Ansi.Strip(cliOutput).Split('\n'))
        {
            var m = RowRegex().Match(line.TrimEnd());
            if (!m.Success) continue;
            result.Add(new Location(
                m.Groups["iso"].Value,
                m.Groups["country"].Value.Trim(),
                m.Groups["city"].Value.Trim(),
                int.Parse(m.Groups["ping"].Value)));
        }
        return result;
    }
}
