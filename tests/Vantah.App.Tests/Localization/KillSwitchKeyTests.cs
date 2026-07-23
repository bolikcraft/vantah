using Vantah.App.Localization;
using Xunit;

public class KillSwitchKeyTests
{
    [Theory]
    [InlineData("ru", "Kill switch (не терять защиту при обрыве)")]
    [InlineData("en", "Kill switch (stay protected if the connection drops)")]
    public void Kill_switch_label_present(string lang, string expected)
    {
        var loc = new Localizer();
        loc.SetLanguage(lang);
        Assert.Equal(expected, loc[LocKeys.Settings_KillSwitch]);
    }
}
