using Vantah.Core.Update;
using Xunit;

public class UpdateCheckParserTests
{
    [Fact]
    public void Latest_version_is_recognized()
    {
        var s = UpdateCheckParser.Parse("You are using the latest version");
        Assert.True(s.IsLatest);
        Assert.Null(s.LatestVersion);
    }

    [Fact]
    public void Latest_is_case_insensitive_and_ignores_ansi_whitespace()
    {
        var s = UpdateCheckParser.Parse("\n  You are using the LATEST version.\n");
        Assert.True(s.IsLatest);
    }

    [Fact]
    public void Available_update_is_flagged_and_version_extracted()
    {
        var s = UpdateCheckParser.Parse("A new version 1.5.2 is available");
        Assert.False(s.IsLatest);
        Assert.Equal("1.5.2", s.LatestVersion);
    }

    [Fact]
    public void Available_update_without_version_still_flagged()
    {
        var s = UpdateCheckParser.Parse("A new version is available, please update");
        Assert.False(s.IsLatest);
        Assert.Null(s.LatestVersion);
    }

    [Fact]
    public void Empty_output_is_treated_as_latest()
    {
        Assert.True(UpdateCheckParser.Parse("").IsLatest);
    }

    [Fact]
    public void Real_update_output_with_the_word_latest_still_reports_the_update()
    {
        // Живой вывод adguardvpn-cli 1.7.13 при доступной 1.8.2. Слово «latest» стоит во фразе
        // про установку обновления и не должно глушить обнаружение новой версии.
        var s = UpdateCheckParser.Parse(
            "> AdGuard VPN v1.8.2 is now available\n" +
            "You can update to the latest version by running `adguardvpn-cli update`");

        Assert.False(s.IsLatest);
        Assert.Equal("1.8.2", s.LatestVersion);
    }
}
