using Vantah.Core.Appearance;

public class WindowOpacityStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "window-opacity");

    [Fact]
    public void Without_a_file_the_default_is_light_transparency()
    {
        Assert.Equal(WindowOpacityStore.Default, new WindowOpacityStore(TempPath()).Load());
    }

    [Fact]
    public void Saved_value_is_read_back()
    {
        var path = TempPath();
        new WindowOpacityStore(path).Save(40);

        Assert.Equal(40, new WindowOpacityStore(path).Load());
    }

    [Theory]
    [InlineData(120, 100)]
    [InlineData(-5, 0)]
    public void Save_clamps_to_the_allowed_range(int given, int expected)
    {
        var path = TempPath();
        new WindowOpacityStore(path).Save(given);

        Assert.Equal(expected, new WindowOpacityStore(path).Load());
    }

    /// <summary>Файл правят руками: мусор и значение вне диапазона не должны гасить окно совсем.</summary>
    [Theory]
    [InlineData("не число")]
    [InlineData("500")]
    [InlineData("")]
    public void Broken_file_falls_back_to_the_default(string content)
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        Assert.Equal(WindowOpacityStore.Default, new WindowOpacityStore(path).Load());
    }
}
