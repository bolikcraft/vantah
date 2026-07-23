using Vantah.App.Localization;
using Vantah.Core.Localization;
using Xunit;

public class DurationKeysTests
{
    [Theory]
    [InlineData("ru", "{0} ч {1} мин", "{0} мин")]
    [InlineData("en", "{0}h {1}m", "{0}m")]
    public void Duration_templates_are_present(string lang, string hm, string m)
    {
        Localizer.Instance.SetLanguage(lang);
        Assert.Equal(hm, Localizer.Instance[LocKeys.Status_DurationHm]);
        Assert.Equal(m, Localizer.Instance[LocKeys.Status_DurationMinutes]);
    }
}
