using System.Threading.Tasks;

namespace Vantah.App.Services;

/// <summary>
/// Раздел, читающий данные через CLI и умеющий перечитать себя после неудачи. Без активного
/// VPN такие чтения нередко не проходят (таймаут), и раздел остаётся с текстом ошибки —
/// SectionReloader дёргает эти разделы, когда VPN подключился.
/// </summary>
public interface IReloadableSection
{
    /// <summary>Идентификатор раздела ("locations" | "domains" | "settings") — для тестов и логов.</summary>
    string Id { get; }

    /// <summary>Последняя загрузка не удалась.</summary>
    bool LoadFailed { get; }

    /// <summary>Перечитать раздел, если прошлая загрузка провалилась; иначе — ничего не делать.</summary>
    Task ReloadIfFailedAsync();
}
