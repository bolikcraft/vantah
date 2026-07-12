namespace Vantah.Core.Models;

public enum SiteExclusionMode { General, Selective }

public static class SiteExclusionModeExtensions
{
    // Аргумент для `site-exclusions mode <...>`.
    public static string ToCliArg(this SiteExclusionMode mode) =>
        mode == SiteExclusionMode.Selective ? "selective" : "general";
}
