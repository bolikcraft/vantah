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

    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    public void System_culture_maps_to_two_letter_code(string system, string expected)
    {
        Assert.Equal(expected, CultureSelector.Resolve(null, new CultureInfo(system)));
    }

    [Fact]
    public void Unsupported_system_culture_falls_back_to_default()
    {
        Assert.Equal("ru", CultureSelector.Resolve(null, new CultureInfo("fr-FR")));
        Assert.Equal("ru", CultureSelector.Default);
    }
}
