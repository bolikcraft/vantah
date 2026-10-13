using Vantah.Core.Update;
using Xunit;

public class AppUpdateInfoTests
{
    private const string Latest = "https://github.com/bolikcraft/vantah/releases/latest";

    [Theory]
    [InlineData("https://github.com/bolikcraft/vantah/releases/tag/v0.2.0")]
    [InlineData("http://example.org/vantah")]
    public void Web_link_is_kept(string url)
    {
        Assert.Equal(new Uri(url), new AppUpdateInfo("v0.2.0", url).ReleasePageUri());
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData("/releases/tag/v0.2.0")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void Broken_or_foreign_link_leads_to_the_latest_release(string url)
    {
        Assert.Equal(new Uri(Latest), new AppUpdateInfo("v0.2.0", url).ReleasePageUri());
    }

    [Fact]
    public void Missing_link_from_a_damaged_file_leads_to_the_latest_release()
    {
        // JSON без поля ссылки даёт null в непустом по типу свойстве.
        Assert.Equal(new Uri(Latest), new AppUpdateInfo("v0.2.0", null!).ReleasePageUri());
    }
}
