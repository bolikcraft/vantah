namespace Vantah.Core.Update;

/// <summary>Найденный релиз Vantah: тег как есть (например «v0.2.0») и ссылка на страницу релиза.</summary>
public sealed record AppUpdateInfo(string Version, string ReleaseUrl)
{
    /// <summary>Страница последнего релиза — запасная ссылка.</summary>
    public const string LatestReleaseUrl = "https://github.com/bolikcraft/vantah/releases/latest";

    /// <summary>
    /// Ссылка для браузера. Релиз читается и из appupdate.json, а файл мог испортиться:
    /// мусор или чужую схему браузеру не отдаём и ведём на последний релиз.
    /// </summary>
    public Uri ReleasePageUri() =>
        Uri.TryCreate(ReleaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri
            : new Uri(LatestReleaseUrl);
}
