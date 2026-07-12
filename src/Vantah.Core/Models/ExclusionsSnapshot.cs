namespace Vantah.Core.Models;

public sealed record ExclusionsSnapshot(SiteExclusionMode Mode, IReadOnlyList<string> Domains)
{
    public static readonly ExclusionsSnapshot Empty =
        new(SiteExclusionMode.General, Array.Empty<string>());
}
