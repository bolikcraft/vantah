namespace Vantah.Core.Notifications;

/// <summary>
/// Что случилось со связью. Событие, а не состояние: обрыв надо отличать от отключения по
/// кнопке, а в снапшоте оба выглядят одинаково — <c>Disconnected</c>. Поэтому источник события
/// называет себя сам: ответы CLI шлёт опрос, а <see cref="UserConnect"/>/<see cref="UserDisconnect"/>
/// — только команды пользователя.
/// </summary>
public enum LinkEvent
{
    /// <summary>CLI ответил «Connected to …»: туннель есть.</summary>
    Up,

    /// <summary>CLI ретраит: «Waiting to reconnect»/«Reconnecting»/«Network lost».</summary>
    Retrying,

    /// <summary>CLI ответил «отключено», и никаких команд от пользователя не было.</summary>
    Down,

    /// <summary>Пользователь (или автоподключение) запросил подключение.</summary>
    UserConnect,

    /// <summary>Пользователь нажал «Отключить».</summary>
    UserDisconnect,
}
