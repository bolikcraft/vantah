using Vantah.Core.Config;

namespace Vantah.Core.Appearance;

/// <summary>
/// Непрозрачность окон в процентах — ~/.config/vantah/window-opacity. Настройка чисто наша,
/// к конфигурации CLI отношения не имеет.
/// </summary>
public sealed class WindowOpacityStore
{
    /// <summary>Значение по умолчанию: лёгкая прозрачность, читаемость ещё не страдает.</summary>
    public const int Default = 92;

    private readonly string _path;

    public WindowOpacityStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "window-opacity");
    }

    /// <summary>Приводит значение к допустимому диапазону 0–100.</summary>
    public static int Clamp(int percent) => Math.Clamp(percent, 0, 100);

    public int Load()
    {
        try
        {
            if (!File.Exists(_path)) return Default;
            // Чужое значение вне диапазона (правка файла руками) — не повод показать окно-невидимку
            // или уронить старт: берём умолчание.
            return int.TryParse(File.ReadAllText(_path).Trim(), out var percent) && percent is >= 0 and <= 100
                ? percent
                : Default;
        }
        catch { return Default; }
    }

    public void Save(int percent)
    {
        SecureFile.WriteAllText(_path, Clamp(percent).ToString());
    }
}
