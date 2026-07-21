using Vantah.Core.Tests.Fakes;
using Vantah.Core.Update;
using Xunit;

public class FallbackReleaseSourceTests
{
    private static readonly AppUpdateInfo FromPrimary = new("v0.2.0", "https://example.invalid/primary");
    private static readonly AppUpdateInfo FromFallback = new("v0.3.0", "https://example.invalid/fallback");

    [Fact]
    public async Task Primary_answer_is_used_and_the_fallback_is_not_touched()
    {
        var fallback = new FakeAppReleaseSource(FromFallback);
        var source = new FallbackReleaseSource(new FakeAppReleaseSource(FromPrimary), fallback);

        var info = await source.GetLatestAsync();

        Assert.Equal("v0.2.0", info?.Version);
        Assert.Equal(0, fallback.Calls);
    }

    [Fact]
    public async Task Silent_primary_hands_over_to_the_fallback()
    {
        // Ровно этот случай: api.github.com ответил 403 по лимиту запросов на IP.
        var source = new FallbackReleaseSource(new FakeAppReleaseSource(null), new FakeAppReleaseSource(FromFallback));

        Assert.Equal("v0.3.0", (await source.GetLatestAsync())?.Version);
    }

    [Fact]
    public async Task Both_silent_is_null()
    {
        var source = new FallbackReleaseSource(new FakeAppReleaseSource(null), new FakeAppReleaseSource(null));

        Assert.Null(await source.GetLatestAsync());
    }
}
