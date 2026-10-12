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

    /// <summary>Подписчик (меню трея) перечитывает файл в обработчике — запись к этому моменту уже на диске.</summary>
    [Fact]
    public void Save_raises_Changed_after_the_file_is_written()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-fav-{Guid.NewGuid():N}.json");
        try
        {
            var store = new FavoritesStore(path);
            var seen = new List<HashSet<string>>();
            store.Changed += (_, _) => seen.Add(store.Load());

            store.Save(new[] { "EE|Tallinn" });

            var loaded = Assert.Single(seen);
            Assert.Equal(new[] { "EE|Tallinn" }, loaded);
        }
        finally { File.Delete(path); }
    }

    /// <summary>Запись не удалась — исключение уходит вызывающему, подписчики ничего не узнают.</summary>
    [Fact]
    public void Save_that_cannot_write_throws_and_does_not_raise_Changed()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vantah-fav-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            // Файл на месте каталога: запись не пройдёт ни у кого, в том числе у root.
            var blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "");
            var store = new FavoritesStore(Path.Combine(blocker, "favorites.json"));
            var raised = 0;
            store.Changed += (_, _) => raised++;

            Assert.ThrowsAny<IOException>(() => store.Save(new[] { "EE|Tallinn" }));
            Assert.Equal(0, raised);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_missing_file_returns_empty()
    {
        var store = new FavoritesStore(Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}.json"));
        Assert.Empty(store.Load());
    }
}
