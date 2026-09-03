using System.Text.RegularExpressions;
using Vantah.Core.Logs;
using Xunit;

public class FileAppLogTests
{
    private const string LinePattern = @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} .+$";

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Disabled_log_does_not_create_the_file()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "app.log");

        var log = new FileAppLog(path);
        log.Write("hello");

        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFileSystemEntries(dir));
    }

    [Fact]
    public void Enabled_log_writes_timestamped_line()
    {
        var path = Path.Combine(TempDir(), "app.log");

        var log = new FileAppLog(path) { Enabled = true };
        log.Write("connect started");

        var lines = File.ReadAllLines(path);
        Assert.Single(lines);
        Assert.Matches(LinePattern, lines[0]);
        Assert.EndsWith(" connect started", lines[0]);
    }

    [Fact]
    public void Turning_off_stops_writing()
    {
        var path = Path.Combine(TempDir(), "app.log");

        var log = new FileAppLog(path) { Enabled = true };
        log.Write("first");
        log.Enabled = false;
        log.Write("second");

        Assert.Single(File.ReadAllLines(path));
    }

    [Fact]
    public void Rotates_into_archive_and_restarts_current_file()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "app.log");

        var log = new FileAppLog(path, maxBytes: 120) { Enabled = true };
        for (var i = 0; i < 4; i++) log.Write($"message-{i}");

        Assert.True(File.Exists(path + ".1"));
        Assert.Contains("message-0", File.ReadAllText(path + ".1"));
        var current = File.ReadAllText(path);
        Assert.DoesNotContain("message-0", current);
        Assert.Contains("message-3", current);
    }

    [Fact]
    public void Second_rotation_overwrites_the_archive()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "app.log");

        var log = new FileAppLog(path, maxBytes: 120) { Enabled = true };
        for (var i = 0; i < 12; i++) log.Write($"message-{i}");

        Assert.Equal(2, Directory.GetFiles(dir).Length);
        Assert.DoesNotContain("message-0", File.ReadAllText(path + ".1"));
    }

    [Fact]
    public void Keeps_size_after_reopening_an_existing_file()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "app.log");

        new FileAppLog(path, maxBytes: 120) { Enabled = true }.Write(new string('a', 100));
        new FileAppLog(path, maxBytes: 120) { Enabled = true }.Write("after restart");

        Assert.True(File.Exists(path + ".1"));
    }

    [Fact]
    public void Concurrent_writes_do_not_interleave()
    {
        var path = Path.Combine(TempDir(), "app.log");
        var log = new FileAppLog(path) { Enabled = true };

        Parallel.For(0, 8, thread =>
        {
            for (var i = 0; i < 25; i++) log.Write($"thread-{thread}-line-{i}");
        });

        var lines = File.ReadAllLines(path);
        Assert.Equal(200, lines.Length);
        Assert.All(lines, line => Assert.Matches(LinePattern, line));
        Assert.Equal(200, lines.Distinct().Count(l => Regex.IsMatch(l, @" thread-\d-line-\d+$")));
    }

    [Fact]
    public void Unwritable_path_does_not_throw()
    {
        var dir = TempDir();
        var blocker = Path.Combine(dir, "blocker");
        File.WriteAllText(blocker, "not a directory");

        // Каталог создать нельзя — на его месте файл.
        var log = new FileAppLog(Path.Combine(blocker, "app.log")) { Enabled = true };
        log.Write("first");
        log.Write("second");
    }

    [Fact]
    public void NullAppLog_stays_disabled_and_writes_nothing()
    {
        IAppLog log = NullAppLog.Instance;
        log.Enabled = true;

        Assert.False(log.Enabled);
        log.Write("ignored");
    }
}
