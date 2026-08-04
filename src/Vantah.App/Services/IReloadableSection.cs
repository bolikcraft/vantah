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

    /// <summary>Текущая (или последняя завершённая) загрузка раздела. Автоподключение на старте
    /// нередко поднимает туннель быстрее, чем стартовое чтение CLI успевает провалиться по
    /// таймауту — переход в Connected застаёт LoadFailed ещё false. SectionReloader ждёт эту
    /// задачу, прежде чем проверять LoadFailed, иначе такой раздел молча пропускается навсегда.</summary>
    Task LoadTask { get; }

    /// <summary>
    /// Перечитать раздел, если прошлая загрузка провалилась; иначе — ничего не делать.
    /// Правит привязанные к UI свойства (Items/IsLoaded/LoadFailed и т. п.), поэтому обязана
    /// вызываться с UI-потока — за маршалинг отвечает вызывающий (SectionReloader маршалит сам,
    /// т.к. AppStateStore.Changed приходит с потока фонового опроса).
    /// </summary>
    Task ReloadIfFailedAsync();
}
