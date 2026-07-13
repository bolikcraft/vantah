using Avalonia.Headless.XUnit;
using Vantah.App.Tray;
using Vantah.Core.Models;

namespace Vantah.App.Tests.Tray;

/// <summary>
/// Зелёная сборка не доказывает, что иконка есть: TrayIconController глушит исключение
/// загрузки и остаётся с пустым треем. Поэтому грузим все ICO по-настоящему, в headless.
/// </summary>
public class TrayIconSetTests
{
    [AvaloniaTheory]
    [InlineData(TrayIconPolarity.Light)]
    [InlineData(TrayIconPolarity.Dark)]
    public void Every_state_has_a_loadable_icon(TrayIconPolarity polarity)
    {
        var set = new TrayIconSet(polarity);

        foreach (var state in Enum.GetValues<ConnectionState>())
            Assert.NotNull(set.For(state));
    }

    [AvaloniaFact]
    public void Error_and_disconnected_share_one_icon_instance()
    {
        var set = new TrayIconSet(TrayIconPolarity.Dark);

        Assert.Same(set.For(ConnectionState.Disconnected), set.For(ConnectionState.Error));
    }
}
