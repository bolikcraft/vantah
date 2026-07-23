namespace Vantah.Core.History;

/// <summary>
/// Человекочитаемая длительность сессии. Шаблоны строк приходят из UI (локализация),
/// поэтому ядро не сочиняет текст для пользователя (конвенция i18n).
/// </summary>
public static class SessionDuration
{
    /// <param name="hoursMinutesTemplate">шаблон с двумя плейсхолдерами, напр. «{0} ч {1} мин».</param>
    /// <param name="minutesTemplate">шаблон с одним плейсхолдером, напр. «{0} мин».</param>
    public static string Format(TimeSpan span, string hoursMinutesTemplate, string minutesTemplate)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        var total = (int)span.TotalMinutes;   // округление вниз до минуты
        var hours = total / 60;
        var minutes = total % 60;
        return hours > 0
            ? string.Format(hoursMinutesTemplate, hours, minutes)
            : string.Format(minutesTemplate, minutes);
    }
}
