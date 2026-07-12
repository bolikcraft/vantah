using Vantah.Core.Favorites;
using Xunit;

public class FavoritesStoreTests
{
    [Fact]
    public void Save_then_Load_roundtrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-fav-{Guid.NewGuid():N}.json");
        try
        {
            var store = new FavoritesStore(path);
            store.Save(new[] { "EE|Tallinn", "NL|Amsterdam" });
            var loaded = new FavoritesStore(path).Load();
            Assert.Contains("EE|Tallinn", loaded);
            Assert.Contains("NL|Amsterdam", loaded);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_missing_file_returns_empty()
    {
        var store = new FavoritesStore(Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}.json"));
        Assert.Empty(store.Load());
    }
}
