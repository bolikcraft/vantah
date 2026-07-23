using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.Core.Tests;

public class KillSwitchStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "killswitch");

    [Fact]
    public void Defaults_to_false_when_file_absent() =>
        Assert.False(new KillSwitchStore(TempPath()).Load());

    [Fact]
    public void Round_trips_true()
    {
        var p = TempPath();
        var store = new KillSwitchStore(p);
        store.Save(true);
        Assert.True(new KillSwitchStore(p).Load());
    }

    [Fact]
    public void Round_trips_false()
    {
        var p = TempPath();
        new KillSwitchStore(p).Save(true);
        new KillSwitchStore(p).Save(false);
        Assert.False(new KillSwitchStore(p).Load());
    }

    [Fact]
    public void Corrupt_content_falls_back_to_false()
    {
        var p = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, "garbage");
        Assert.False(new KillSwitchStore(p).Load());
    }
}
