using System.Globalization;

namespace Vantah.Core.Localization;

/// <summary>Выбор культуры интерфейса: сохранённый выбор → системная локаль → дефолт.</summary>
public static class CultureSelector
{
    /// <summary>Поддерживаемые языки (двухбуквенные ISO). Новый язык — .resx плюс код сюда.</summary>
    public static readonly string[] Supported = { "ru", "en" };

    /// <summary>Язык по умолчанию, когда ни сохранённый выбор, ни системная локаль не подошли.</summary>
    public const string Default = "ru";

    /// <summary>
    /// Возвращает двухбуквенный код языка интерфейса. Сохранённый пользователем выбор побеждает,
    /// если он поддерживается; иначе берётся язык системной UI-культуры, а при его отсутствии
    /// в списке поддерживаемых — <see cref="Default"/>.
    /// </summary>
    public static string Resolve(string? persisted, CultureInfo? systemUi = null)
    {
        if (IsSupported(persisted)) return persisted!;

        var system = (systemUi ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;
        return IsSupported(system) ? system : Default;
    }

    private static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        Supported.Contains(code, StringComparer.OrdinalIgnoreCase);
}
