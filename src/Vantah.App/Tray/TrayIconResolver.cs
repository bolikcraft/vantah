using Vantah.Core.Models;

namespace Vantah.App.Tray;

/// <summary>Комплект иконок трея: светлый знак — для тёмных панелей, тёмный — для светлых.</summary>
public enum TrayIconPolarity { Light, Dark }

/// <summary>
/// Чистая логика иконки трея: какой глиф показывать в каком состоянии и каким комплектом.
/// Avalonia кладёт в трей растр, а не symbolic-иконку, — панель его не перекрашивает,
/// поэтому комплект приходится выбирать самим.
/// </summary>
public static class TrayIconResolver
{
    /// <summary>Ключ INI (~/.config/vantah/vantah.conf): auto | light | dark.</summary>
    public const string PolarityKey = "tray_icon";

    public static string GlyphName(ConnectionState state) => state switch
    {
        ConnectionState.Connected => "connected",
        ConnectionState.Connecting or ConnectionState.Disconnecting => "connecting",
        // Disconnected и Error: защиты нет. Отдельный аварийный глиф на 16px не читается,
        // а причина ошибки уходит в тултип.
        _ => "disconnected",
    };

    /// <param name="configured">Значение ключа tray_icon или null, если не задано.</param>
    /// <param name="appThemeIsDark">Тёмная ли тема приложения — по ней угадываем цвет панели.</param>
    public static TrayIconPolarity ResolvePolarity(string? configured, bool appThemeIsDark) =>
        configured?.Trim().ToLowerInvariant() switch
        {
            "light" => TrayIconPolarity.Light,
            "dark" => TrayIconPolarity.Dark,
            // Всё прочее, включая «auto» и мусор в конфиге, — автоматика.
            _ => appThemeIsDark ? TrayIconPolarity.Light : TrayIconPolarity.Dark,
        };

    public static string AssetUri(ConnectionState state, TrayIconPolarity polarity) =>
        $"avares://Vantah.App/Assets/tray/{polarity.ToString().ToLowerInvariant()}-{GlyphName(state)}.ico";
}
