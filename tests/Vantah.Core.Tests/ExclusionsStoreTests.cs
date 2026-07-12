using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Xunit;

public class ExclusionsStoreTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), $"vantah-excl-{Guid.NewGuid():N}");

    [Fact]
    public void Save_then_Load_roundtrips_per_mode()
    {
        var dir = TempDir();
        try
        {
            var store = new ExclusionsStore(dir);
            store.Save(SiteExclusionMode.General, new[] { "example.com", "foo.net" });
            store.Save(SiteExclusionMode.Selective, new[] { "bank.example" });

            Assert.Equal(new[] { "example.com", "foo.net" }, new ExclusionsStore(dir).Load(SiteExclusionMode.General));
            Assert.Equal(new[] { "bank.example" }, new ExclusionsStore(dir).Load(SiteExclusionMode.Selective));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_missing_file_returns_empty()
    {
        var store = new ExclusionsStore(TempDir());
        Assert.Empty(store.Load(SiteExclusionMode.General));
    }

    [Fact]
    public void Save_normalizes_before_writing()
    {
        var dir = TempDir();
        try
        {
            var store = new ExclusionsStore(dir);
            store.Save(SiteExclusionMode.General, new[] { " a.com ", "A.COM", "", "b.com" });
            Assert.Equal(new[] { "a.com", "b.com" }, store.Load(SiteExclusionMode.General));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Export_then_Import_roundtrips()
    {
        var dir = TempDir();
        var file = Path.Combine(dir, "list.vantah");
        try
        {
            Directory.CreateDirectory(dir);
            var store = new ExclusionsStore(dir);
            store.Export(file, new[] { "x.com", "y.com" });
            Assert.Equal(new[] { "x.com", "y.com" }, store.Import(file));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
