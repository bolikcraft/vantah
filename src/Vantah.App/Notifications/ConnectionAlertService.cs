using System;
using System.Threading.Tasks;
using Vantah.App.Localization;
using Vantah.Core.Notifications;

namespace Vantah.App.Notifications;

/// <summary>
/// Связывает события связи с плашками рабочего стола: ядро отдаёт код события, строку
/// собираем здесь и на текущем языке — в момент показа, поэтому смена языка применяется сразу.
/// </summary>
public sealed class ConnectionAlertService : ILinkObserver
{
    private readonly IDesktopNotifier _notifier;
    private readonly NotificationsStore _settings;
    private readonly ConnectionAlertDecider _decider;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();

    public ConnectionAlertService(
        IDesktopNotifier notifier,
        NotificationsStore? settings = null,
        ConnectionAlertDecider? decider = null,
        Func<DateTimeOffset>? clock = null)
    {
        _notifier = notifier;
        _settings = settings ?? new NotificationsStore();
        _decider = decider ?? new ConnectionAlertDecider();
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Последняя отправка — точка ожидания для тестов и диагностики.</summary>
    public Task LastNotifyTask { get; private set; } = Task.CompletedTask;

    public void Observe(LinkEvent link)
    {
        ConnectionAlert alert;
        lock (_gate) alert = _decider.Observe(link, _clock());
        if (alert == ConnectionAlert.None) return;

        // Тумблер читаем перед самой отправкой: выключенный — и на шину не ходим вовсе.
        // Состояние при этом уже обновлено, поэтому включение на середине обрыва не выдаст
        // задним числом плашку о том, что произошло, пока уведомления были выключены.
        if (!_settings.Load()) return;

        var (summary, body) = Texts(alert);
        // Отправку уводим в пул: Observe зовут из опроса статуса на UI-потоке, и ждать шину там
        // нельзя. Исключений отсюда не бывает — NotifyAsync молчит на любом отказе.
        LastNotifyTask = Task.Run(() => _notifier.NotifyAsync(summary, body));
    }

    private static (string Summary, string Body) Texts(ConnectionAlert alert)
    {
        var loc = Localizer.Instance;
        return alert == ConnectionAlert.Lost
            ? (loc[LocKeys.Notify_ConnectionLost], loc[LocKeys.Notify_ConnectionLostBody])
            : (loc[LocKeys.Notify_ConnectionRestored], loc[LocKeys.Notify_ConnectionRestoredBody]);
    }
}
