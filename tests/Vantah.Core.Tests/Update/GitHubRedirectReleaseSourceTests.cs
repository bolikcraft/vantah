using System.Net;
using Vantah.Core.Update;
using Xunit;

/// <summary>
/// Запасной источник: web-морда github.com не подчиняется лимиту api.github.com (60 запросов
/// в час на IP), а он бьёт как раз по нашей аудитории — общим выходным адресам VPN.
/// </summary>
public class GitHubRedirectReleaseSourceTests
{
    private sealed class StubHandler(HttpStatusCode code, string? location) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            var response = new HttpResponseMessage(code);
            if (location is not null) response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task Reads_the_tag_from_the_redirect()
    {
        var source = new GitHubRedirectReleaseSource("0.1.0", new StubHandler(
            HttpStatusCode.Found, "https://github.com/bolikcraft/vantah/releases/tag/v0.2.0"));

        var info = await source.GetLatestAsync();

        Assert.Equal("v0.2.0", info?.Version);
        Assert.Equal("https://github.com/bolikcraft/vantah/releases/tag/v0.2.0", info?.ReleaseUrl);
    }

    [Fact]
    public async Task Handles_a_relative_location()
    {
        var source = new GitHubRedirectReleaseSource("0.1.0", new StubHandler(
            HttpStatusCode.Found, "/bolikcraft/vantah/releases/tag/0.2.0"));

        var info = await source.GetLatestAsync();

        Assert.Equal("0.2.0", info?.Version);
        Assert.Equal("https://github.com/bolikcraft/vantah/releases/tag/0.2.0", info?.ReleaseUrl);
    }

    [Fact]
    public async Task Asks_the_releases_latest_page_without_following_the_redirect()
    {
        var handler = new StubHandler(HttpStatusCode.Found,
            "https://github.com/bolikcraft/vantah/releases/tag/v0.2.0");
        var source = new GitHubRedirectReleaseSource("0.1.0", handler);

        await source.GetLatestAsync();

        Assert.Equal("https://github.com/bolikcraft/vantah/releases/latest",
            handler.Last?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Head, handler.Last?.Method);
    }

    [Fact]
    public async Task Private_or_missing_repository_is_null()
    {
        var source = new GitHubRedirectReleaseSource("0.1.0", new StubHandler(HttpStatusCode.NotFound, null));

        Assert.Null(await source.GetLatestAsync());
    }

    [Fact]
    public async Task Redirect_without_a_location_is_null()
    {
        var source = new GitHubRedirectReleaseSource("0.1.0", new StubHandler(HttpStatusCode.Found, null));

        Assert.Null(await source.GetLatestAsync());
    }

    [Fact]
    public async Task Redirect_to_a_page_without_a_tag_is_null()
    {
        // Репозиторий без релизов уводит на список, а не на страницу тега.
        var source = new GitHubRedirectReleaseSource("0.1.0", new StubHandler(
            HttpStatusCode.Found, "https://github.com/bolikcraft/vantah/releases"));

        Assert.Null(await source.GetLatestAsync());
    }
}
