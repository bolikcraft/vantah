using Vantah.Core.Update;
using Xunit;

public class AppUpdateStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "appupdate.json");

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var state = new AppUpdateStore(TempPath()).Load();

        Assert.True(state.Enabled);
        Assert.Null(state.LastCheckUtc);
        Assert.Null(state.DismissedVersion);
    }

    [Fact]
    public void Round_trips_all_fields()
    {
        var path = TempPath();
        var store = new AppUpdateStore(path);
        var when = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);

        store.Save(new AppUpdateState
        {
            Enabled = false,
            LastCheckUtc = when,
            DismissedVersion = "v0.2.0",
        });

        var loaded = new AppUpdateStore(path).Load();
        Assert.False(loaded.Enabled);
        Assert.Equal(when, loaded.LastCheckUtc);
        Assert.Equal("v0.2.0", loaded.DismissedVersion);
    }

    [Fact]
    public void Garbage_file_gives_defaults()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");

        Assert.True(new AppUpdateStore(path).Load().Enabled);
    }
}
