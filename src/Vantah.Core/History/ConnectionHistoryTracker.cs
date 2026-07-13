namespace Vantah.Core.History;

/// <summary>
/// Ведёт активную сессию VPN и список завершённых. Логика старта/финализации/смены локации.
/// Время передаётся параметром <c>now</c> — класс не читает системные часы сам (тестируемость).
/// <para>
/// Идентичность сессии — это ГОРОД. Страна и пинг приезжают позже (из list-locations) и являются
/// обогащением, а не признаком смены локации: их дозаполняем в активной записи на месте.
/// </para>
/// <para>
/// Завершённые сессии персистятся в <see cref="ConnectionHistoryStore"/>, активная —
/// в <see cref="ActiveSessionStore"/>, поэтому переживает перезапуск приложения.
/// </para>
/// </summary>
public sealed class ConnectionHistoryTracker
{
    /// <summary>Как часто heartbeat активной сессии доходит до диска (опрос-то идёт каждые 4с).</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    private readonly ConnectionHistoryStore _store;
    private readonly ActiveSessionStore _activeStore;
    private readonly List<ConnectionHistoryEntry> _completed;
    private readonly object _gate = new();

    private ConnectionHistoryEntry? _active;
    private DateTimeOffset _lastSeenAt;        // когда активную сессию последний раз видели живой
    private DateTimeOffset _persistedLastSeen; // какой heartbeat уже лежит на диске
    // Активная сессия поднята с диска и ещё не подтверждена наблюдением в этом процессе.
    // Такую сессию нельзя закрывать по «сейчас» — только по _lastSeenAt: между падением/закрытием
    // приложения и его новым стартом могли пройти часы, и мы не знаем, был ли VPN жив всё это время.
    private bool _restored;

    public ConnectionHistoryTracker(ConnectionHistoryStore store, ActiveSessionStore activeStore)
    {
        _store = store;
        _activeStore = activeStore;
        _completed = _store.Load().ToList();

        if (_activeStore.Load() is { } state)
        {
            _active = state.Entry;
            _lastSeenAt = state.LastSeenAt;
            _persistedLastSeen = state.LastSeenAt;
            _restored = true;
        }
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
            if (_active is { } a && SameCity(a.City, city))
            {
                // Тот же город: сессия продолжается (в т.ч. восстановленная после перезапуска —
                // тогда ничего нового не пишем, сохраняя её исходный StartedAt).
                _restored = false;
                var enriched = Enrich(a, city, country, ping);
                _active = enriched;
                _lastSeenAt = now;
                // Троттлинг heartbeat: на диск — только если запись реально изменилась
                // или прошло >= HeartbeatInterval с прошлой записи.
                if (!ReferenceEquals(enriched, a) || now - _persistedLastSeen >= HeartbeatInterval)
                    PersistActiveLocked();
                return;
            }

            if (_active is not null)
                FinalizeLocked(now); // смена локации: закрыть предыдущую

            _active = new ConnectionHistoryEntry(city, country, ping, now, EndedAt: null);
            _lastSeenAt = now;
            _restored = false;
            PersistActiveLocked();
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

    // Страна/пинг могут приехать позже города: дозаполняем, но никогда не затираем
    // уже известные значения пустышками. Написание города берём из обогащённого
    // источника («Amsterdam» из списка локаций вместо «AMSTERDAM» из status).
    private static ConnectionHistoryEntry Enrich(
        ConnectionHistoryEntry a, string city, string country, int ping)
    {
        var newCity = string.IsNullOrEmpty(country) ? a.City : city;
        var newCountry = string.IsNullOrEmpty(country) ? a.Country : country;
        var newPing = ping > 0 ? ping : a.Ping;

        if (newCity == a.City && newCountry == a.Country && newPing == a.Ping)
            return a; // ничего не изменилось — тот же экземпляр, писать на диск нечего

        return a with { City = newCity, Country = newCountry, Ping = newPing };
    }

    // Сессию, поднятую с диска, закрываем последним моментом, когда мы её видели живой,
    // а наблюдаемую в этом процессе — текущим временем.
    private void FinalizeLocked(DateTimeOffset now)
    {
        var endedAt = _restored ? _lastSeenAt : now;
        var ended = _active! with { EndedAt = endedAt };
        _completed.Insert(0, ended); // newest-first
        if (_completed.Count > ConnectionHistoryStore.MaxEntries)
            _completed.RemoveRange(
                ConnectionHistoryStore.MaxEntries,
                _completed.Count - ConnectionHistoryStore.MaxEntries);
        _active = null;
        _restored = false;
        _store.Save(_completed);
        _activeStore.Clear();
    }

    private void PersistActiveLocked()
    {
        _activeStore.Save(new ActiveSessionState(_active!, _lastSeenAt));
        _persistedLastSeen = _lastSeenAt;
    }

    private static bool SameCity(string city1, string city2) =>
        string.Equals(city1, city2, StringComparison.OrdinalIgnoreCase);
}
