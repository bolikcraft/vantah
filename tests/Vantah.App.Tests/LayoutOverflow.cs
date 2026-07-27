using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Vantah.App.Tests;

/// <summary>
/// Поиск обрезанных подписей в живом визуальном дереве.
/// </summary>
/// <remarks>
/// Считать символы бесполезно: обрежется подпись или нет, решают метрики шрифта. Поэтому
/// смотрим на реально измеренную раскладку — сколько места тексту выдали (<c>Bounds</c>) и
/// сколько он просит, если его не ограничивать.
///
/// Второе приходится измерять заново: <c>DesiredSize</c> у <c>TextBlock</c> уже обрезан по
/// доступной ширине (у кнопки шириной 180 подпись «Прервать подключение» отдаёт ровно 142 —
/// столько, сколько влезло), так что сравнение <c>DesiredSize</c> с <c>Bounds</c> всегда
/// сходится и не доказывает ничего. Настоящую ширину даёт повторный <c>Measure</c> без
/// ограничений.
///
/// Строки с переносом (<c>TextWrapping</c>) и с явным многоточием (<c>TextTrimming</c>)
/// пропускаем: там усечение — осознанное решение разметки.
/// </remarks>
public static class LayoutOverflow
{
    /// <summary>Обрезанная подпись: что за текст, по какой оси, сколько просил и сколько получил.</summary>
    public readonly record struct Clipped(string Text, string Axis, double Wanted, double Given)
    {
        public override string ToString() =>
            $"«{Text}»: {Axis} нужно {Wanted:0.#} px, дали {Given:0.#} px";
    }

    // Пол-пикселя: раскладка Avalonia округляет размеры по масштабу экрана.
    private const double Tolerance = 0.5;

    /// <summary>
    /// Все видимые подписи под <paramref name="root"/>, которым не хватило ширины.
    /// Вызывать после прохода раскладки (<c>Window.UpdateLayout</c>).
    /// </summary>
    public static IReadOnlyList<Clipped> Find(Visual root)
    {
        var found = new List<Clipped>();
        var remeasured = new List<Layoutable>();

        foreach (var text in root.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!text.IsEffectivelyVisible || string.IsNullOrEmpty(text.Text)) continue;
            if (text.TextWrapping != TextWrapping.NoWrap) continue;
            if (text.TextTrimming != TextTrimming.None) continue;

            var given = text.Bounds.Size;

            text.Measure(Size.Infinity);
            remeasured.Add(text);
            // DesiredSize включает Margin, Bounds — нет: приводим к одной системе координат.
            var wanted = new Size(
                text.DesiredSize.Width - text.Margin.Left - text.Margin.Right,
                text.DesiredSize.Height - text.Margin.Top - text.Margin.Bottom);

            if (wanted.Width - given.Width > Tolerance)
                found.Add(new Clipped(text.Text!, "по ширине", wanted.Width, given.Width));
            // Фиксированная высота строки списка режет текст не хуже фиксированной ширины.
            if (wanted.Height - given.Height > Tolerance)
                found.Add(new Clipped(text.Text!, "по высоте", wanted.Height, given.Height));
        }

        // Мы мерили не теми ограничениями, что раскладка: помечаем задетое как грязное,
        // чтобы следующий проход посчитал всё заново, а не взял наш результат из кэша.
        foreach (var control in remeasured) control.InvalidateMeasure();

        return found;
    }

    /// <summary>Читаемый список находок для сообщения об упавшем тесте.</summary>
    public static string Describe(IEnumerable<Clipped> clipped) =>
        string.Join("; ", clipped.Select(c => c.ToString()));
}
