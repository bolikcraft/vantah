using System.Text.RegularExpressions;

namespace Vantah.Core.Cli;

public static partial class Ansi
{
    [GeneratedRegex("\\x1b\\[[0-9;]*m")]
    private static partial Regex EscapeRegex();

    public static string Strip(string input) => EscapeRegex().Replace(input, string.Empty);
}
