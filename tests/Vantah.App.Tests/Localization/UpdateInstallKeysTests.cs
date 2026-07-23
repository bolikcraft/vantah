using Vantah.App.Localization;
using Xunit;

public class UpdateInstallKeysTests
{
    [Theory]
    [InlineData("ru", "Settings_InstallUpdate", "Обновить сейчас")]
    [InlineData("en", "Settings_InstallUpdate", "Update now")]
    [InlineData("ru", "Settings_UpdateDone", "Обновлено. Перезапустите Vantah, чтобы применить.")]
    [InlineData("en", "Settings_UpdateDone", "Updated. Restart Vantah to apply.")]
    [InlineData("ru", "Settings_UpdateUpToDate", "У вас уже последняя версия")]
    [InlineData("en", "Settings_UpdateUpToDate", "You already have the latest version")]
    public void Keys_present(string lang, string key, string expected)
    {
        var loc = new Localizer();
        loc.SetLanguage(lang);
        Assert.Equal(expected, loc[key]);
    }
}
