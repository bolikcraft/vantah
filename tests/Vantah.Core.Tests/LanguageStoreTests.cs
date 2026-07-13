using Vantah.Core.Localization;
using Xunit;

public class LanguageStoreTests
{
    [Fact]
    public void Save_then_Load_roundtrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-lang-{Guid.NewGuid():N}");
        try
        {
            new LanguageStore(path).Save("en");
            Assert.Equal("en", new LanguageStore(path).Load());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_missing_file_returns_null()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-lang-nope-{Guid.NewGuid():N}");
        Assert.Null(new LanguageStore(path).Load());
    }

    [Fact]
    public void Load_blank_file_returns_null()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-lang-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(path, "  \n");
            Assert.Null(new LanguageStore(path).Load());
        }
        finally { File.Delete(path); }
    }
}
