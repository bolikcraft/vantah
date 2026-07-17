using Vantah.Core.Models;
using Vantah.Core.Vpn;
using Xunit;

public class IpVersionStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "ip-version");

    [Fact]
    public void Default_is_auto_when_no_file()
    {
        var store = new IpVersionStore(TempPath());
        Assert.Equal(IpVersionPreference.Auto, store.Load());
    }

    [Fact]
    public void Round_trips_the_saved_value()
    {
        var path = TempPath();
        new IpVersionStore(path).Save(IpVersionPreference.IPv6Only);
        Assert.Equal(IpVersionPreference.IPv6Only, new IpVersionStore(path).Load());
    }

    [Fact]
    public void Garbage_falls_back_to_auto()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "nonsense");
        Assert.Equal(IpVersionPreference.Auto, new IpVersionStore(path).Load());
    }
}
