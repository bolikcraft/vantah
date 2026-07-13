using Vantah.Core.History;
using Xunit;

public class ConnectionHistoryStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"vantah-hist-{Guid.NewGuid():N}");

    private static ConnectionHistoryEntry Entry(string city, DateTimeOffset start, DateTimeOffset? end) =>
        new(city, "Netherlands", 24, start, end);

    [Fact]
    public void Save_then_Load_roundtrips_including_nullable_EndedAt()
    {
        var path = TempFile();
        try
        {
            var start = new DateTimeOffset(2026, 07, 12, 10, 00, 00, TimeSpan.Zero);
            var end   = start.AddMinutes(30);
            var store = new ConnectionHistoryStore(path);

            store.Save(new[]
            {
                Entry("Amsterdam", start, end),   // завершённая
                Entry("Oslo", start, null),       // ещё активная (EndedAt = null)
            });

            var loaded = new ConnectionHistoryStore(path).Load();
            Assert.Equal(2, loaded.Count);
            Assert.Equal("Amsterdam", loaded[0].City);
            Assert.Equal("Netherlands", loaded[0].Country);
            Assert.Equal(24, loaded[0].Ping);
            Assert.Equal(start, loaded[0].StartedAt);
            Assert.Equal(end, loaded[0].EndedAt);
            Assert.Null(loaded[1].EndedAt);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_missing_file_returns_empty()
    {
        var store = new ConnectionHistoryStore(TempFile());
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Save_is_json_lines_one_entry_per_line()
    {
        var path = TempFile();
        try
        {
            var start = DateTimeOffset.UnixEpoch;
            new ConnectionHistoryStore(path).Save(new[]
            {
                Entry("A", start, start), Entry("B", start, start), Entry("C", start, start),
            });
            var lines = File.ReadAllLines(path).Where(l => l.Length > 0).ToArray();
            Assert.Equal(3, lines.Length);
            Assert.All(lines, l => Assert.StartsWith("{", l.TrimStart()));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_and_Load_cap_at_12_keeping_first_entries()
    {
        var path = TempFile();
        try
        {
            var start = DateTimeOffset.UnixEpoch;
            var many = Enumerable.Range(0, 20)
                .Select(i => Entry($"City{i}", start, start)).ToArray();
            new ConnectionHistoryStore(path).Save(many);

            var loaded = new ConnectionHistoryStore(path).Load();
            Assert.Equal(12, loaded.Count);
            Assert.Equal("City0", loaded[0].City);
            Assert.Equal("City11", loaded[11].City);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_skips_corrupt_lines()
    {
        var path = TempFile();
        try
        {
            var start = DateTimeOffset.UnixEpoch;
            new ConnectionHistoryStore(path).Save(new[] { Entry("Good", start, start) });
            File.AppendAllText(path, "not-json\n\n");
            var loaded = new ConnectionHistoryStore(path).Load();
            Assert.Single(loaded);
            Assert.Equal("Good", loaded[0].City);
        }
        finally { File.Delete(path); }
    }
}
