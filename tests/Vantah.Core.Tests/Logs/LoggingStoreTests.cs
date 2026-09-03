using Vantah.Core.Logs;
using Xunit;

public class LoggingStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "logging");

    [Fact]
    public void Defaults_to_false_when_file_absent() =>
        Assert.False(new LoggingStore(TempPath()).Load());

    [Fact]
    public void Round_trips_true()
    {
        var p = TempPath();
        new LoggingStore(p).Save(true);
        Assert.True(new LoggingStore(p).Load());
    }

    [Fact]
    public void Round_trips_false()
    {
        var p = TempPath();
        new LoggingStore(p).Save(true);
        new LoggingStore(p).Save(false);
        Assert.False(new LoggingStore(p).Load());
    }

    [Fact]
    public void Corrupt_content_falls_back_to_false()
    {
        var p = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, "garbage");
        Assert.False(new LoggingStore(p).Load());
    }
}
