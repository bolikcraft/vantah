namespace Vantah.Core.Notifications;

/// <summary>Приёмник событий связи. Реализация решает, показывать ли уведомление.</summary>
public interface ILinkObserver
{
    /// <summary>Вызывается из опроса статуса, поэтому обязан вернуться сразу и не бросать.</summary>
    void Observe(LinkEvent link);
}
