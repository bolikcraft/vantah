namespace Vantah.Core.Update;

/// <summary>
/// Разбор тега релиза GitHub («v0.2.0») в <see cref="Version"/>. Предрелизных суффиксов у
/// Vantah нет, поэтому хвост после «-» или «+» просто отбрасываем, а не сравниваем по semver.
/// </summary>
public static class ReleaseTagParser
{
    public static Version? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var s = tag.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];

        var cut = s.IndexOfAny(['-', '+']);
        if (cut >= 0) s = s[..cut];

        return Version.TryParse(s, out var v) ? v : null;
    }
}
