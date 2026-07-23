using Vantah.App.Localization;
using Xunit;

public class DurationKeysTests
{
    // Собственный экземпляр Localizer (как в LocalizerTests), а не Localizer.Instance:
    // синглтон общий на весь тест-хост, и смена языка протекла бы в соседние тесты.
    [Theory]
    [InlineData("ru", "{0} ч {1} мин", "{0} мин")]
    [InlineData("en", "{0}h {1}m", "{0}m")]
    public void Duration_templates_are_present(string lang, string hm, string m)
    {
        var loc = new Localizer();
        loc.SetLanguage(lang);
        Assert.Equal(hm, loc[LocKeys.Status_DurationHm]);
        Assert.Equal(m, loc[LocKeys.Status_DurationMinutes]);
    }
}
