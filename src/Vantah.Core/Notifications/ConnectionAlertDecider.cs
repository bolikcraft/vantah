namespace Vantah.Core.Notifications;

/// <summary>
/// Решает, когда сказать про обрыв и про восстановление. Уведомляем ровно два случая:
/// связь пропала сама и связь вернулась после такого обрыва. Подключение и отключение
/// по кнопке пользователь и так видит на экране.
/// </summary>
/// <remarks>Не потокобезопасен: вызывающий сериализует обращения.</remarks>
public sealed class ConnectionAlertDecider
{
    /// <summary>Как мы понимаем текущее состояние связи.</summary>
    private enum Link
    {
        /// <summary>Туннеля нет и не ждём: ещё не подключались либо отключил сам пользователь.</summary>
        Idle,
        Up,
        /// <summary>Туннель был и пропал сам.</summary>
        Broken,
    }

    /// <summary>
    /// Сколько новое состояние должно продержаться, прежде чем о нём сказать. Опрос идёт раз
    /// в 4 секунды, и восьми секунд хватает на два подряд одинаковых ответа CLI. Так метания
    /// связи (kill switch успевает потерять и поднять туннель между опросами) не порождают
    /// ни одной плашки, а настоящий обрыв виден со второго ответа.
    /// </summary>
    public static readonly TimeSpan DefaultSettleWindow = TimeSpan.FromSeconds(8);

    private readonly TimeSpan _settleWindow;

    private Link _link = Link.Idle;
    private DateTimeOffset _since = DateTimeOffset.MinValue;
    private bool _lostAnnounced;

    public ConnectionAlertDecider(TimeSpan? settleWindow = null) =>
        _settleWindow = settleWindow ?? DefaultSettleWindow;

    public ConnectionAlert Observe(LinkEvent link, DateTimeOffset now)
    {
        switch (link)
        {
            // Команда пользователя закрывает инцидент: дальше он смотрит на приложение сам.
            case LinkEvent.UserConnect:
            case LinkEvent.UserDisconnect:
                SetLink(Link.Idle, now);
                _lostAnnounced = false;
                return ConnectionAlert.None;

            case LinkEvent.Up:
                SetLink(Link.Up, now);
                break;

            default:
                // Туннеля нет. Это обрыв, только если туннель был: иначе это обычное
                // «отключено», о котором сообщать нечего.
                if (_link != Link.Idle) SetLink(Link.Broken, now);
                break;
        }

        if (now - _since < _settleWindow) return ConnectionAlert.None;

        if (_link == Link.Broken && !_lostAnnounced)
        {
            _lostAnnounced = true;
            return ConnectionAlert.Lost;
        }

        if (_link == Link.Up && _lostAnnounced)
        {
            _lostAnnounced = false;
            return ConnectionAlert.Restored;
        }

        return ConnectionAlert.None;
    }

    // Отсчёт «сколько состояние держится» перезапускаем только на смене состояния: повторные
    // одинаковые ответы CLI его не сбрасывают, иначе окно никогда бы не истекало.
    private void SetLink(Link value, DateTimeOffset now)
    {
        if (_link == value) return;
        _link = value;
        _since = now;
    }
}
