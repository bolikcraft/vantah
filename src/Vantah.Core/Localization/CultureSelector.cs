using System.Globalization;

namespace Vantah.Core.Localization;

/// <summary>Выбор культуры интерфейса: сохранённый выбор → системная локаль → дефолт.</summary>
public static class CultureSelector
{
    /// <summary>
    /// Поддерживаемые языки. Коды — имена культур .NET, не обязательно двухбуквенные: у
    /// португальского и китайского вариант значим (бразильский, упрощённое письмо), и имя файла
    /// ресурсов (<c>Strings.pt-BR.resx</c>) должно совпадать с кодом отсюда. Новый язык —
    /// .resx плюс код сюда.
    /// </summary>
    public static readonly string[] Supported =
        { "ru", "en", "de", "fr", "es", "it", "pl", "uk", "tr", "pt-BR", "zh-Hans", "id" };

    /// <summary>Язык по умолчанию, когда ни сохранённый выбор, ни системная локаль не подошли.</summary>
    public const string Default = "en";

    /// <summary>
    /// Возвращает код языка интерфейса. Сохранённый пользователем выбор побеждает, если он
    /// поддерживается; иначе берётся системная UI-культура, а при её отсутствии в списке —
    /// <see cref="Default"/>. Системная культура матчится тремя шагами: точное имя
    /// (<c>pt-BR</c>), затем язык без региона (<c>de-AT</c> → <c>de</c>), затем поддерживаемый
    /// вариант того же языка (<c>pt-PT</c> → <c>pt-BR</c>, <c>zh-CN</c> → <c>zh-Hans</c>):
    /// чужой вариант родного языка всегда ближе пользователю, чем английский по умолчанию.
    /// </summary>
    public static string Resolve(string? persisted, CultureInfo? systemUi = null)
    {
        if (Match(persisted) is { } chosen) return chosen;

        var system = systemUi ?? CultureInfo.CurrentUICulture;
        return Match(system.Name)
            ?? Match(system.TwoLetterISOLanguageName)
            ?? Variant(system.TwoLetterISOLanguageName)
            ?? Default;
    }

    /// <summary>Точное совпадение с поддерживаемым кодом (регистр не важен).</summary>
    private static string? Match(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Supported.FirstOrDefault(s => string.Equals(s, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Поддерживаемый вариант того же языка: «pt» → «pt-BR».</summary>
    private static string? Variant(string language) =>
        Supported.FirstOrDefault(s => s.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase));
}
