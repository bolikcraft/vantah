namespace Vantah.Core.History;

/// <summary>Одна сессия VPN: локация и временной диапазон. EndedAt = null — сессия ещё активна.</summary>
public sealed record ConnectionHistoryEntry(
    string City,
    string Country,
    int Ping,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);
