using System.Globalization;
using Vantah.Core.Localization;
using Xunit;

public class CultureSelectorTests
{
    [Fact]
    public void Persisted_supported_choice_wins_over_system()
    {
        Assert.Equal("en", CultureSelector.Resolve("en", new CultureInfo("ru-RU")));
    }

    [Fact]
    public void Persisted_unsupported_choice_is_ignored()
    {
        Assert.Equal("en", CultureSelector.Resolve("eo", new CultureInfo("en-US")));
    }

    [Fact]
    public void Persisted_regional_choice_survives_a_restart()
    {
        // Коды вроде «pt-BR»/«zh-Hans» пишутся в ~/.config/vantah/language как есть
        // и должны подниматься обратно без потери варианта.
        Assert.Equal("pt-BR", CultureSelector.Resolve("pt-BR", new CultureInfo("en-US")));
        Assert.Equal("zh-Hans", CultureSelector.Resolve("zh-Hans", new CultureInfo("en-US")));
    }

    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("de-AT", "de")]      // регион отбрасывается, язык поддержан
    [InlineData("pt-BR", "pt-BR")]   // точное совпадение с региональным кодом
    public void System_culture_maps_to_supported_code(string system, string expected)
    {
        Assert.Equal(expected, CultureSelector.Resolve(null, new CultureInfo(system)));
    }

    [Theory]
    [InlineData("pt-PT", "pt-BR")]    // европейский португальский → бразильский
    [InlineData("zh-CN", "zh-Hans")]  // китайский любого региона → упрощённое письмо
    [InlineData("zh-TW", "zh-Hans")]
    public void System_culture_falls_back_to_a_supported_variant_of_the_same_language(string system, string expected)
    {
        // Чужой вариант родного языка ближе пользователю, чем русский по умолчанию.
        Assert.Equal(expected, CultureSelector.Resolve(null, new CultureInfo(system)));
    }

    [Fact]
    public void Unsupported_system_culture_falls_back_to_default()
    {
        Assert.Equal("ru", CultureSelector.Resolve(null, new CultureInfo("ja-JP")));
        Assert.Equal("ru", CultureSelector.Default);
    }
}
