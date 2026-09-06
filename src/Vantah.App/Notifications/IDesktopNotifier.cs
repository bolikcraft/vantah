using System.Threading;
using System.Threading.Tasks;

namespace Vantah.App.Notifications;

/// <summary>
/// Показ плашки рабочего стола. За интерфейсом прячется D-Bus: тесты решения «когда
/// уведомлять» не должны требовать живой сессионной шины.
/// </summary>
public interface IDesktopNotifier
{
    /// <summary>Показывает плашку, заменяя предыдущую. Не бросает: нет шины — нет уведомления.</summary>
    Task NotifyAsync(string summary, string body, CancellationToken ct = default);
}
