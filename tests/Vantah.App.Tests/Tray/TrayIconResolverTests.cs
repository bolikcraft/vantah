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
    // Статус ещё не опрошен — серый глиф, как и «отключено».
    [InlineData(ConnectionState.Unknown, "disconnected")]
    public void Glyph_matches_state(ConnectionState state, string expected)
    {
        Assert.Equal(expected, TrayIconResolver.GlyphName(state));
    }

    [Fact]
    public void Asset_uri_is_built_from_state()
    {
        Assert.Equal(
            "avares://Vantah.App/Assets/tray/connected.ico",
            TrayIconResolver.AssetUri(ConnectionState.Connected));
        // Ошибка берёт файл «отключено», а не свой собственный.
        Assert.Equal(
            "avares://Vantah.App/Assets/tray/disconnected.ico",
            TrayIconResolver.AssetUri(ConnectionState.Error));
    }
}
