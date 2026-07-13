using Vantah.Core.History;
using Xunit;

public class ConnectionHistoryTrackerTests
{
    private static (ConnectionHistoryTracker tracker, string path) NewTracker()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-hist-{Guid.NewGuid():N}");
        return (new ConnectionHistoryTracker(new ConnectionHistoryStore(path)), path);
    }

    private static DateTimeOffset At(int minute) =>
        new(2026, 07, 12, 10, minute, 00, TimeSpan.Zero);

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
        finally { File.Delete(path); }
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
        finally { File.Delete(path); }
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
        finally { File.Delete(path); }
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
        finally { File.Delete(path); }
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
        finally { File.Delete(path); }
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
        finally { File.Delete(path); }
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
        finally { File.Delete(path); }
    }

    [Fact]
    public void Finalized_history_survives_new_tracker_via_store()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-hist-{Guid.NewGuid():N}");
        try
        {
            var t1 = new ConnectionHistoryTracker(new ConnectionHistoryStore(path));
            t1.OnConnected("Amsterdam", "Netherlands", 24, At(0));
            t1.OnDisconnected(At(30));

            var t2 = new ConnectionHistoryTracker(new ConnectionHistoryStore(path));
            Assert.Single(t2.Previous);
            Assert.Equal("Amsterdam", t2.Previous[0].City);
        }
        finally { File.Delete(path); }
    }
}
