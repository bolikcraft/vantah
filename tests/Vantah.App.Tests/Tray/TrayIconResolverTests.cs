using Vantah.App.Tray;
using Vantah.Core.Models;

namespace Vantah.App.Tests.Tray;

public class TrayIconResolverTests
{
    [Theory]
    [InlineData(ConnectionState.Connected, "connected")]
    [InlineData(ConnectionState.Connecting, "connecting")]
    [InlineData(ConnectionState.Disconnecting, "connecting")]
    [InlineData(ConnectionState.Disconnected, "disconnected")]
    // Ошибка рисуется как «отключено»: туннеля нет, и иконка не должна намекать на обратное.
    [InlineData(ConnectionState.Error, "disconnected")]
    public void Glyph_matches_state(ConnectionState state, string expected)
    {
        Assert.Equal(expected, TrayIconResolver.GlyphName(state));
    }

    [Theory]
    [InlineData("light", TrayIconPolarity.Light)]
    [InlineData("dark", TrayIconPolarity.Dark)]
    [InlineData("LIGHT", TrayIconPolarity.Light)]
    [InlineData("  dark  ", TrayIconPolarity.Dark)]
    public void Explicit_config_wins(string configured, TrayIconPolarity expected)
    {
        Assert.Equal(expected, TrayIconResolver.ResolvePolarity(configured, appThemeIsDark: true));
        Assert.Equal(expected, TrayIconResolver.ResolvePolarity(configured, appThemeIsDark: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("auto")]
    [InlineData("чепуха")]
    public void Auto_takes_polarity_opposite_to_theme(string? configured)
    {
        // Тёмная тема → тёмная панель → знак должен быть светлым, и наоборот.
        Assert.Equal(TrayIconPolarity.Light, TrayIconResolver.ResolvePolarity(configured, appThemeIsDark: true));
        Assert.Equal(TrayIconPolarity.Dark, TrayIconResolver.ResolvePolarity(configured, appThemeIsDark: false));
    }

    [Fact]
    public void Asset_uri_is_built_from_polarity_and_state()
    {
        Assert.Equal(
            "avares://Vantah.App/Assets/tray/light-connected.ico",
            TrayIconResolver.AssetUri(ConnectionState.Connected, TrayIconPolarity.Light));
        Assert.Equal(
            "avares://Vantah.App/Assets/tray/dark-disconnected.ico",
            TrayIconResolver.AssetUri(ConnectionState.Error, TrayIconPolarity.Dark));
    }
}
