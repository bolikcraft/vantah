using Vantah.Core.Models;
using Vantah.Core.Vpn;
using Xunit;

public class AutoConnectStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "autoconnect");

    [Theory]
    [InlineData(AutoConnectMode.Off)]
    [InlineData(AutoConnectMode.LastUsed)]
    [InlineData(AutoConnectMode.Fastest)]
    public void Round_trips_each_value(AutoConnectMode mode)
    {
        var store = new AutoConnectStore(TempPath());
        store.Save(mode);
        Assert.Equal(mode, store.Load());
    }

    [Fact]
    public void Missing_file_is_off()
    {
        Assert.Equal(AutoConnectMode.Off, new AutoConnectStore(TempPath()).Load());
    }

    [Fact]
    public void Garbage_is_off()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "nonsense");
        Assert.Equal(AutoConnectMode.Off, new AutoConnectStore(path).Load());
    }
}
