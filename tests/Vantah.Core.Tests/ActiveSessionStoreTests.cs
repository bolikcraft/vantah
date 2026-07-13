using Vantah.Core.History;
using Xunit;

public class ActiveSessionStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"vantah-active-{Guid.NewGuid():N}");

    [Fact]
    public void Save_then_Load_roundtrips_entry_and_heartbeat()
    {
        var path = TempFile();
        try
        {
            var start = new DateTimeOffset(2026, 07, 12, 10, 00, 00, TimeSpan.Zero);
            var seen = start.AddHours(3);
            new ActiveSessionStore(path).Save(new ActiveSessionState(
                new ConnectionHistoryEntry("Amsterdam", "Netherlands", 24, start, EndedAt: null),
                seen));

            var loaded = new ActiveSessionStore(path).Load();
            Assert.NotNull(loaded);
            Assert.Equal(seen, loaded!.LastSeenAt);
            Assert.Equal("Amsterdam", loaded.Entry.City);
            Assert.Equal("Netherlands", loaded.Entry.Country);
            Assert.Equal(24, loaded.Entry.Ping);
            Assert.Equal(start, loaded.Entry.StartedAt);
            Assert.Null(loaded.Entry.EndedAt);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_missing_file_returns_null()
    {
        Assert.Null(new ActiveSessionStore(TempFile()).Load());
    }

    [Fact]
    public void Load_corrupt_file_returns_null()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, "not-json");
            Assert.Null(new ActiveSessionStore(path).Load());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Clear_removes_the_file_and_is_idempotent()
    {
        var path = TempFile();
        try
        {
            var store = new ActiveSessionStore(path);
            store.Save(new ActiveSessionState(
                new ConnectionHistoryEntry("Oslo", "Norway", 40, DateTimeOffset.UnixEpoch, null),
                DateTimeOffset.UnixEpoch));
            Assert.True(File.Exists(path));

            store.Clear();
            Assert.False(File.Exists(path));
            store.Clear(); // повторный вызов не должен падать
            Assert.Null(store.Load());
        }
        finally { File.Delete(path); }
    }
}
