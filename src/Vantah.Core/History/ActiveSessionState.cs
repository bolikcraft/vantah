namespace Vantah.Core.History;

/// <summary>
/// Активная (незавершённая) сессия VPN на диске: сама запись + heartbeat.
/// <paramref name="LastSeenAt"/> — последний момент, когда Vantah видел это подключение живым.
/// Именно им закрывается сессия, восстановленная после перезапуска: «сейчас» соврало бы,
/// приписав сессии всё время, пока приложение было закрыто.
/// </summary>
public sealed record ActiveSessionState(
    ConnectionHistoryEntry Entry,
    DateTimeOffset LastSeenAt);
