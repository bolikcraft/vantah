// Vantah — только Linux, поэтому Unix-права доступны всегда (CA1416 предупреждает про Windows).
#pragma warning disable CA1416

using Vantah.Core.Update;
using Xunit;

public sealed class AppUpdateStoreTests : IDisposable
{
    private const UnixFileMode Private600 = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode Private700 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"vantah-appupdate-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string TempPath() =>
        Path.Combine(_root, Guid.NewGuid().ToString("N"), "appupdate.json");

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

    [Fact]
    public void Save_creates_file_readable_only_by_owner()
    {
        var path = TempPath();
        new AppUpdateStore(path).Save(new AppUpdateState());

        Assert.Equal(Private600, File.GetUnixFileMode(path));
    }

    [Fact]
    public void Save_creates_directory_traversable_only_by_owner()
    {
        var path = TempPath();
        new AppUpdateStore(path).Save(new AppUpdateState());

        Assert.Equal(Private700, File.GetUnixFileMode(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void Save_leaves_no_temp_files_behind()
    {
        var path = TempPath();
        new AppUpdateStore(path).Save(new AppUpdateState());

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }
}
