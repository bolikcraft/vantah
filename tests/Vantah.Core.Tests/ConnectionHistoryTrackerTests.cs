using Vantah.Core.History;
using Xunit;

public class ConnectionHistoryTrackerTests
{
    private static (ConnectionHistoryTracker tracker, string path) NewTracker()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-hist-{Guid.NewGuid():N}");
        return (NewTracker(path), path);
    }

    // Трекеру нужны два файла: завершённая история и активная сессия. Второй кладём рядом,
    // чтобы «перезапуск приложения» в тестах = новый трекер поверх тех же путей.
    private static ConnectionHistoryTracker NewTracker(string path) =>
        new(new ConnectionHistoryStore(path), new ActiveSessionStore(path + ".active"));

    private static void Cleanup(string path)
    {
        File.Delete(path);
        File.Delete(path + ".active");
    }

    // Момент времени = база 2026-07-12 10:00 + minute минут (minute может быть > 59).
    private static DateTimeOffset At(int minute) =>
        new DateTimeOffset(2026, 07, 12, 10, 00, 00, TimeSpan.Zero).AddMinutes(minute);

    [Fact]
    public void Active_session_is_not_in_Previous_until_finalized()
    {
        var (t, path) = NewTracker();
        try
        {
            t.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            Assert.Empty(t.Previous);
            Assert.NotNull(t.Active);
            Assert.Equal("Amsterdam", t.Active!.City);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Connect_then_Disconnect_produces_one_finalized_entry()
    {
        var (t, path) = NewTracker();
        try
        {
            t.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t.OnDisconnected(At(30));

            Assert.Null(t.Active);
            Assert.Single(t.Previous);
            var e = t.Previous[0];
            Assert.Equal("Amsterdam", e.City);
            Assert.Equal(At(0), e.StartedAt);
            Assert.Equal(At(30), e.EndedAt);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Reconnecting_same_location_case_insensitive_is_noop()
    {
        var (t, path) = NewTracker();
        try
        {
            t.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t.OnConnected("AMSTERDAM", "netherlands", 24, At(5)); // тот же город/страна, другой регистр
            Assert.Empty(t.Previous);
            Assert.Equal(At(0), t.Active!.StartedAt); // старт не сброшен
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Country_enrichment_of_same_city_does_not_split_session()
    {
        var (t, path) = NewTracker();
        try
        {
            // На старте список локаций ещё не загружен: страна пустая, пинг 0.
            t.OnConnected("AMSTERDAM", "", 0, At(0));
            // Через ~4 секунды приехал list-locations — тот же город, но уже со страной и пингом.
            t.OnConnected("Amsterdam", "Netherlands", 24, At(1));

            Assert.Empty(t.Previous);                        // сессия НЕ разорвана
            Assert.NotNull(t.Active);
            Assert.Equal(At(0), t.Active!.StartedAt);        // старт сохранён
            Assert.Equal("Netherlands", t.Active.Country);   // страна дозаполнена на месте
            Assert.Equal(24, t.Active.Ping);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Switching_location_finalizes_previous_and_starts_new()
    {
        var (t, path) = NewTracker();
        try
        {
            t.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t.OnConnected("Oslo", "Norway", 40, At(10));

            Assert.Single(t.Previous);
            Assert.Equal("Amsterdam", t.Previous[0].City);
            Assert.Equal(At(10), t.Previous[0].EndedAt); // финализирована моментом переключения
            Assert.Equal("Oslo", t.Active!.City);
            Assert.Null(t.Active.EndedAt);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Newest_finalized_session_is_first()
    {
        var (t, path) = NewTracker();
        try
        {
            t.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t.OnDisconnected(At(5));
            t.OnConnected("Oslo", "Norway", 40, At(6));
            t.OnDisconnected(At(9));

            Assert.Equal("Oslo", t.Previous[0].City);      // новее — сверху
            Assert.Equal("Amsterdam", t.Previous[1].City);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void History_is_capped_at_12()
    {
        var (t, path) = NewTracker();
        try
        {
            for (var i = 0; i < 15; i++)
            {
                t.OnConnected($"City{i}", "X", i, At(0));
                t.OnDisconnected(At(1));
            }
            Assert.Equal(12, t.Previous.Count);
            Assert.Equal("City14", t.Previous[0].City);  // самая новая
            Assert.Equal("City3", t.Previous[11].City);  // 15 - 12 = отброшены City0..City2
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Disconnect_without_active_session_is_noop()
    {
        var (t, path) = NewTracker();
        try
        {
            t.OnDisconnected(At(0));
            Assert.Empty(t.Previous);
            Assert.Null(t.Active);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Finalized_history_survives_new_tracker_via_store()
    {
        var (t1, path) = NewTracker();
        try
        {
            t1.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t1.OnDisconnected(At(30));

            var t2 = NewTracker(path);
            Assert.Single(t2.Previous);
            Assert.Equal("Amsterdam", t2.Previous[0].City);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Explicit_disconnect_in_this_process_finalizes_with_now()
    {
        var (t, path) = NewTracker();
        try
        {
            t.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t.OnConnected("Amsterdam", "Netherlands", 24, At(40)); // heartbeat: сессия жива
            t.OnDisconnected(At(50));

            // Сессию наблюдали живьём в этом процессе — закрываем «сейчас», а не heartbeat'ом.
            Assert.Equal(At(50), t.Previous[0].EndedAt);
        }
        finally { Cleanup(path); }
    }

    // --- Перезапуск приложения: активная сессия поднимается с диска ---

    [Fact]
    public void Restart_with_same_city_resumes_session_without_writing_history()
    {
        var (t1, path) = NewTracker();
        try
        {
            t1.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t1.OnConnected("Amsterdam", "Netherlands", 24, At(40)); // heartbeat

            var t2 = NewTracker(path); // «перезапуск»: сессия поднята с диска
            t2.OnConnected("AMSTERDAM", "", 0, At(100)); // первый опрос: локации ещё не загружены

            Assert.Empty(t2.Previous);                     // ничего нового в историю не пишем
            Assert.NotNull(t2.Active);
            Assert.Equal(At(0), t2.Active!.StartedAt);     // исходный старт сохранён
            Assert.Equal("Netherlands", t2.Active.Country); // пустая страна не затирает известную
            Assert.Equal(24, t2.Active.Ping);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Restart_while_disconnected_finalizes_restored_session_with_LastSeenAt()
    {
        var (t1, path) = NewTracker();
        try
        {
            t1.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t1.OnConnected("Amsterdam", "Netherlands", 24, At(40)); // heartbeat: last-seen = 10:40

            var t2 = NewTracker(path);
            t2.OnDisconnected(At(100)); // VPN уже не поднят; «сейчас» = 11:40

            Assert.Null(t2.Active);
            Assert.Single(t2.Previous);
            Assert.Equal(At(0), t2.Previous[0].StartedAt);
            Assert.Equal(At(40), t2.Previous[0].EndedAt); // честный last-seen, а не «сейчас»
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Restart_with_different_city_finalizes_restored_with_LastSeenAt_and_opens_new()
    {
        var (t1, path) = NewTracker();
        try
        {
            t1.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t1.OnConnected("Amsterdam", "Netherlands", 24, At(40)); // heartbeat: last-seen = 10:40

            var t2 = NewTracker(path);
            t2.OnConnected("Oslo", "Norway", 40, At(100));

            Assert.Single(t2.Previous);
            Assert.Equal("Amsterdam", t2.Previous[0].City);
            Assert.Equal(At(40), t2.Previous[0].EndedAt); // закрыта по last-seen
            Assert.Equal("Oslo", t2.Active!.City);
            Assert.Equal(At(100), t2.Active.StartedAt);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Resumed_session_is_finalized_with_now_after_being_seen_alive()
    {
        var (t1, path) = NewTracker();
        try
        {
            t1.OnConnected("Amsterdam", "Netherlands", 24, At(0)); // last-seen = 10:00

            var t2 = NewTracker(path);
            t2.OnConnected("Amsterdam", "Netherlands", 24, At(30)); // подтвердили: сессия жива
            t2.OnDisconnected(At(50));

            // После подтверждения живьём сессия перестаёт быть «восстановленной»:
            // закрываем текущим временем, а не heartbeat'ом.
            Assert.Equal(At(50), t2.Previous[0].EndedAt);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Finalized_session_clears_the_active_session_file()
    {
        var (t, path) = NewTracker();
        try
        {
            t.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            Assert.True(File.Exists(path + ".active"));

            t.OnDisconnected(At(30));
            Assert.False(File.Exists(path + ".active")); // активной сессии больше нет

            var t2 = NewTracker(path);
            Assert.Null(t2.Active); // и она не воскресает на следующем старте
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Heartbeat_is_throttled_and_not_written_on_every_poll()
    {
        var (t, path) = NewTracker();
        var active = path + ".active";
        try
        {
            t.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            var afterStart = File.GetLastWriteTimeUtc(active);
            var written = File.ReadAllText(active);

            // Опросы каждые 4 секунды в пределах окна троттлинга — на диск не ходим.
            for (var i = 4; i <= 28; i += 4)
                t.OnConnected("Amsterdam", "Netherlands", 24, At(0).AddSeconds(i));
            Assert.Equal(written, File.ReadAllText(active));
            Assert.Equal(afterStart, File.GetLastWriteTimeUtc(active));

            // Прошло >= 30 секунд — heartbeat уезжает на диск.
            t.OnConnected("Amsterdam", "Netherlands", 24, At(0).AddSeconds(32));
            var state = new ActiveSessionStore(active).Load();
            Assert.Equal(At(0).AddSeconds(32), state!.LastSeenAt);
            Assert.Equal(At(0), state.Entry.StartedAt);
        }
        finally { Cleanup(path); }
    }

    // --- Диск отвалился: персист истории — best-effort, не должен ронять соединение ---

    // Родительский каталог путей сторов на самом деле является ФАЙЛОМ: Directory.CreateDirectory
    // внутри Store.Save() бросит IOException. Так проверяем реальное поведение без моков.
    private static (ConnectionHistoryTracker tracker, string root) NewTrackerWithBrokenDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var blocker = Path.Combine(root, "blocker");
        File.WriteAllText(blocker, ""); // это ФАЙЛ, не каталог

        var histStore = new ConnectionHistoryStore(Path.Combine(blocker, "hist"));
        var activeStore = new ActiveSessionStore(Path.Combine(blocker, "active"));
        return (new ConnectionHistoryTracker(histStore, activeStore), root);
    }

    [Fact]
    public void OnConnected_swallows_store_io_failure()
    {
        var (tracker, root) = NewTrackerWithBrokenDisk();
        try
        {
            var ex = Record.Exception(() => tracker.OnConnected("Amsterdam", "NL", 10, DateTimeOffset.UtcNow));

            Assert.Null(ex);                    // персист упал, но наружу не бросили
            Assert.NotNull(tracker.Active);      // in-memory состояние в норме
            Assert.Equal("Amsterdam", tracker.Active!.City);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void OnDisconnected_swallows_store_io_failure()
    {
        var (tracker, root) = NewTrackerWithBrokenDisk();
        try
        {
            tracker.OnConnected("Amsterdam", "NL", 10, At(0)); // персист активной сессии тоже упадёт

            var ex = Record.Exception(() => tracker.OnDisconnected(At(30)));

            Assert.Null(ex);                     // финализация (Store.Save истории) не бросает
            Assert.Null(tracker.Active);
            Assert.Single(tracker.Previous);      // in-memory история обновлена, несмотря на сбой диска
            Assert.Equal("Amsterdam", tracker.Previous[0].City);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
