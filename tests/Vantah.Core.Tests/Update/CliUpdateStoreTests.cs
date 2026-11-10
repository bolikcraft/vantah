using Vantah.Core.Update;
using Xunit;

public sealed class CliUpdateStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"vantah-cliupdate-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string TempPath() =>
        Path.Combine(_root, Guid.NewGuid().ToString("N"), "cliupdate.json");

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var state = new CliUpdateStore(TempPath()).Load();

        Assert.Null(state.DismissedVersion);
    }

    [Fact]
    public void Round_trips_the_dismissed_version()
    {
        var path = TempPath();
        new CliUpdateStore(path).Update(_ => new CliUpdateState { DismissedVersion = "2.3.1" });

        Assert.Equal("2.3.1", new CliUpdateStore(path).Load().DismissedVersion);
    }

    [Fact]
    public void Garbage_file_gives_defaults()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");

        Assert.Null(new CliUpdateStore(path).Load().DismissedVersion);
    }
}
