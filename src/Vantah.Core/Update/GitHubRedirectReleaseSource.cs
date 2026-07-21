namespace Vantah.Core.Update;

/// <summary>
/// Запасной источник последнего релиза: страница github.com/.../releases/latest отвечает
/// редиректом на страницу тега, и тег читается прямо из заголовка Location. Нужен потому, что
/// api.github.com без токена ограничен 60 запросами в час НА IP, а пользователи Vantah сидят
/// за общими выходными адресами VPN и упираются в этот лимит из-за чужого трафика.
/// Web-морда таким лимитом не связана, но и данных даёт меньше — отсюда роль запасного.
/// </summary>
public sealed class GitHubRedirectReleaseSource : IAppReleaseSource
{
    private const string LatestReleaseUrl = "https://github.com/bolikcraft/vantah/releases/latest";

    /// <summary>Метка страницы тега: всё, что после неё — сам тег.</summary>
    private const string TagPathMarker = "/releases/tag/";

    private readonly HttpClient _http;

    /// <param name="appVersion">Версия Vantah для User-Agent.</param>
    /// <param name="handler">Подменяется в тестах; в приложении — null.</param>
    public GitHubRedirectReleaseSource(string appVersion, HttpMessageHandler? handler = null)
    {
        // Редирект нам нужен сырым: следовать за ним незачем, тег лежит в Location.
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false });
        _http.Timeout = TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Vantah/{appVersion}");
    }

    public async Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default)
    {
        try
        {
            // HEAD: тело страницы релиза нам не нужно, интересен только заголовок.
            using var request = new HttpRequestMessage(HttpMethod.Head, LatestReleaseUrl);
            using var response = await _http.SendAsync(request, ct);

            if (response.Headers.Location is not { } location) return null;

            // Location бывает и относительным — достраиваем от адреса запроса.
            var url = location.IsAbsoluteUri ? location : new Uri(new Uri(LatestReleaseUrl), location);

            var path = url.ToString();
            var marker = path.IndexOf(TagPathMarker, StringComparison.Ordinal);
            // Репозиторий без релизов уводит на список — там метки тега нет.
            if (marker < 0) return null;

            var tag = path[(marker + TagPathMarker.Length)..].Trim('/');
            if (tag.Length == 0) return null;

            return new AppUpdateInfo(Uri.UnescapeDataString(tag), path);
        }
        catch
        {
            // Сеть, таймаут, отсутствие DNS — проверка просто не состоялась, молча.
            return null;
        }
    }
}
