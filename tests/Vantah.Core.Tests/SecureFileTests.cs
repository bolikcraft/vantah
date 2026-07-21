// Vantah — только Linux, поэтому Unix-права доступны всегда (CA1416 предупреждает про Windows).
#pragma warning disable CA1416

using Vantah.Core.Config;
using Xunit;

public sealed class SecureFileTests : IDisposable
{
    private const UnixFileMode Private600 = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode Private700 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode Open755 =
        Private700 |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"vantah-securefile-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string TempPath() =>
        Path.Combine(_root, Guid.NewGuid().ToString("N"), "data", "file");

    [Fact]
    public void WriteAllText_creates_file_readable_only_by_owner()
    {
        var path = TempPath();
        SecureFile.WriteAllText(path, "secret-ish");
        Assert.Equal("secret-ish", File.ReadAllText(path));
        Assert.Equal(Private600, File.GetUnixFileMode(path));
    }

    [Fact]
    public void WriteAllText_creates_directory_traversable_only_by_owner()
    {
        var path = TempPath();
        SecureFile.WriteAllText(path, "x");
        Assert.Equal(Private700, File.GetUnixFileMode(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void WriteAllText_overwrites_existing_file_and_keeps_600()
    {
        var path = TempPath();
        SecureFile.WriteAllText(path, "first");
        SecureFile.WriteAllText(path, "second");
        Assert.Equal("second", File.ReadAllText(path));
        Assert.Equal(Private600, File.GetUnixFileMode(path));
    }

    [Fact]
    public void WriteAllLines_writes_lines_with_600()
    {
        var path = TempPath();
        SecureFile.WriteAllLines(path, new[] { "a", "b" });
        Assert.Equal(new[] { "a", "b" }, File.ReadAllLines(path));
        Assert.Equal(Private600, File.GetUnixFileMode(path));
    }

    [Fact]
    public void No_temp_files_left_behind()
    {
        var path = TempPath();
        SecureFile.WriteAllText(path, "x");
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void WriteAllText_tightens_existing_directory_left_over_from_older_versions()
    {
        var path = TempPath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.SetUnixFileMode(dir, Open755);

        SecureFile.WriteAllText(path, "x");

        Assert.Equal(Private700, File.GetUnixFileMode(dir));
    }

    [Fact]
    public void WriteAllText_does_not_touch_shared_sticky_directory()
    {
        var path = TempPath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var shared =
            UnixFileMode.StickyBit | Private700 |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(dir, shared);

        SecureFile.WriteAllText(path, "x");

        Assert.Equal(shared, File.GetUnixFileMode(dir));
        Assert.Equal(Private600, File.GetUnixFileMode(path));
    }

    /// <summary>
    /// Экспорт в чужой каталог (secureDirectory: false): каталог остаётся 755, но ни временный,
    /// ни итоговый файл не наследуют umask — оба создаются сразу 600.
    /// </summary>
    [Fact]
    public void WriteAllText_in_open_directory_never_widens_file_beyond_600()
    {
        var path = TempPath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.SetUnixFileMode(dir, Open755);

        SecureFile.WriteAllText(path, "exported", secureDirectory: false);

        Assert.Equal("exported", File.ReadAllText(path));
        Assert.Equal(Private600, File.GetUnixFileMode(path));
        Assert.Equal(Open755, File.GetUnixFileMode(dir));
    }

    [Fact]
    public void WriteAllLines_in_open_directory_never_widens_file_beyond_600()
    {
        var path = TempPath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.SetUnixFileMode(dir, Open755);

        SecureFile.WriteAllLines(path, new[] { "a" }, secureDirectory: false);

        Assert.Equal(Private600, File.GetUnixFileMode(path));
    }
}
