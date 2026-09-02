using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vantah.Core.Appearance;

namespace Vantah.App.Services;

/// <summary>
/// Прозрачность окон. Полупрозрачным делаем только фон — текст и элементы остаются плотными.
/// Фон всех окон рисует одна общая кисть из ресурсов приложения (ключ <see cref="BrushKey"/>),
/// поэтому менять прозрачность на лету достаточно у неё: окна перекрашиваются сами, реестр
/// открытых окон вести не нужно.
/// </summary>
public sealed class WindowTransparency(WindowOpacityStore store)
{
    /// <summary>Ключ общей кисти фона окон в <c>App.axaml</c>.</summary>
    public const string BrushKey = "VantahWindowBackground";

    /// <summary>Текущая непрозрачность в процентах (0–100).</summary>
    public int Percent { get; private set; } = store.Load();

    /// <summary>
    /// Разрешает окну прозрачный фон. Зовётся из конструктора окна: визуал под прозрачность
    /// выбирается при создании окна, и выставить уровень стилем (то есть уже после показа) поздно.
    /// Без композитора в системе уровень останется <c>None</c>, и окно будет обычным —
    /// подложку в этом случае рисует TransparencyBackgroundFallback (стиль в App.axaml).
    /// </summary>
    public static void Enable(Window window) =>
        window.TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

    /// <summary>Применяет текущее значение к общей кисти (вызывается на старте приложения).</summary>
    public void Apply()
    {
        if (Brush() is { } brush) brush.Opacity = Percent / 100.0;
    }

    /// <summary>Меняет непрозрачность: запоминает на следующий запуск и применяет сразу.</summary>
    public void Set(int percent)
    {
        Percent = WindowOpacityStore.Clamp(percent);
        store.Save(Percent);
        Apply();
    }

    // Ресурсов может не быть только в тестах вьюмодели без поднятого приложения — там менять нечего.
    private static SolidColorBrush? Brush() =>
        Application.Current is { } app && app.Resources.TryGetResource(BrushKey, null, out var value)
            ? value as SolidColorBrush
            : null;
}
