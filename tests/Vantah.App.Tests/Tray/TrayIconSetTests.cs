using Avalonia.Headless.XUnit;
using Vantah.App.Tray;
using Vantah.Core.Models;

namespace Vantah.App.Tests.Tray;

/// <summary>
/// Зелёная сборка не доказывает, что иконка есть: путь avares:// — строка, и опечатка в нём
/// или невключённый в ресурсы ICO компилируются молча. Поэтому грузим все ICO по-настоящему,
/// через реальный загрузчик ресурсов Avalonia, в headless.
/// </summary>
public class TrayIconSetTests
{
    [AvaloniaFact]
    public void Every_state_has_a_loadable_icon()
    {
        var set = new TrayIconSet();

        foreach (var state in Enum.GetValues<ConnectionState>())
            Assert.NotNull(set.For(state));
    }

    [AvaloniaFact]
    public void Error_and_disconnected_share_one_icon_instance()
    {
        var set = new TrayIconSet();

        Assert.Same(set.For(ConnectionState.Disconnected), set.For(ConnectionState.Error));
    }
}
