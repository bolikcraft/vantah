using Vantah.Core.Vpn;
using Xunit;

public class LastLocationStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "last-location");

    [Fact]
    public void Round_trips_a_location()
    {
        var store = new LastLocationStore(TempPath());
        store.Save("Amsterdam");
        Assert.Equal("Amsterdam", store.Load());
    }

    [Fact]
    public void Missing_is_null()
    {
        Assert.Null(new LastLocationStore(TempPath()).Load());
    }

    [Fact]
    public void Blank_is_not_written()
    {
        var path = TempPath();
        var store = new LastLocationStore(path);
        store.Save("   ");
        Assert.False(File.Exists(path));
        Assert.Null(store.Load());
    }
}
