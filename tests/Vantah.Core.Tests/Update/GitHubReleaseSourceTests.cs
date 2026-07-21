using System.Net;
using Vantah.Core.Update;
using Xunit;

public class GitHubReleaseSourceTests
{
    // Урезанный до используемых полей ответ GitHub API на /releases/latest.
    private const string Sample = """
    {
      "tag_name": "v0.2.0",
      "name": "Vantah v0.2.0",
      "draft": false,
      "prerelease": false,
      "html_url": "https://github.com/bolikcraft/vantah/releases/tag/v0.2.0"
    }
    """;

    private sealed class StubHandler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }

    [Fact]
    public async Task Reads_tag_and_url_from_the_response()
    {
        var source = new GitHubReleaseSource("0.1.0", new StubHandler(HttpStatusCode.OK, Sample));

        var info = await source.GetLatestAsync();

        Assert.Equal("v0.2.0", info?.Version);
        Assert.Equal("https://github.com/bolikcraft/vantah/releases/tag/v0.2.0", info?.ReleaseUrl);
    }

    [Fact]
    public async Task Requests_the_latest_release_endpoint_with_a_user_agent()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Sample);
        var source = new GitHubReleaseSource("0.1.0", handler);

        await source.GetLatestAsync();

        Assert.Equal(
            "https://api.github.com/repos/bolikcraft/vantah/releases/latest",
            handler.Last?.RequestUri?.ToString());
        // Без User-Agent GitHub отвечает 403 — заголовок обязателен.
        Assert.Contains("Vantah", handler.Last!.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task Error_response_is_null()
    {
        var source = new GitHubReleaseSource("0.1.0", new StubHandler(HttpStatusCode.Forbidden, ""));

        Assert.Null(await source.GetLatestAsync());
    }

    [Fact]
    public async Task Broken_json_is_null()
    {
        var source = new GitHubReleaseSource("0.1.0", new StubHandler(HttpStatusCode.OK, "{ not json"));

        Assert.Null(await source.GetLatestAsync());
    }

    [Fact]
    public async Task Response_without_a_tag_is_null()
    {
        var source = new GitHubReleaseSource("0.1.0", new StubHandler(HttpStatusCode.OK, """{"name":"x"}"""));

        Assert.Null(await source.GetLatestAsync());
    }
}
