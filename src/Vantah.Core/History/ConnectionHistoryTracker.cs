namespace Vantah.Core.History;

/// <summary>
/// Ведёт активную сессию VPN и список завершённых. Логика старта/финализации/смены локации.
/// Время передаётся параметром <c>now</c> — класс не читает системные часы сам (тестируемость).
/// Завершённые сессии персистятся в <see cref="ConnectionHistoryStore"/>; активная — нет.
/// </summary>
public sealed class ConnectionHistoryTracker
{
    private readonly ConnectionHistoryStore _store;
    private readonly List<ConnectionHistoryEntry> _completed;
    private readonly object _gate = new();
    private ConnectionHistoryEntry? _active;

    public ConnectionHistoryTracker(ConnectionHistoryStore store)
    {
        _store = store;
        _completed = _store.Load().ToList();
    }

    /// <summary>Текущая незавершённая сессия (или null).</summary>
    public ConnectionHistoryEntry? Active
    {
        get { lock (_gate) return _active; }
    }

    /// <summary>Завершённые сессии, newest-first, кап 12. Активная сюда не входит.</summary>
    public IReadOnlyList<ConnectionHistoryEntry> Previous
    {
        get { lock (_gate) return _completed.ToArray(); }
    }

    public void OnConnected(string city, string country, int ping, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_active is { } a && SameLocation(a.City, a.Country, city, country))
                return; // уже отслеживаем эту локацию

            if (_active is not null)
                FinalizeLocked(now); // смена локации: закрыть предыдущую

            _active = new ConnectionHistoryEntry(city, country, ping, now, EndedAt: null);
        }
    }

    public void OnDisconnected(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_active is null) return;
            FinalizeLocked(now);
        }
    }

    private void FinalizeLocked(DateTimeOffset now)
    {
        var ended = _active! with { EndedAt = now };
        _completed.Insert(0, ended); // newest-first
        if (_completed.Count > ConnectionHistoryStore.MaxEntries)
            _completed.RemoveRange(
                ConnectionHistoryStore.MaxEntries,
                _completed.Count - ConnectionHistoryStore.MaxEntries);
        _active = null;
        _store.Save(_completed);
    }

    private static bool SameLocation(string city1, string country1, string city2, string country2) =>
        string.Equals(city1, city2, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(country1, country2, StringComparison.OrdinalIgnoreCase);
}
