// Vantah — только Linux, поэтому Unix-права доступны всегда (CA1416 предупреждает про Windows).
#pragma warning disable CA1416

using Vantah.Core.Config;
using Xunit;

public class SecureFileTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "data", "file");

    [Fact]
    public void WriteAllText_creates_file_readable_only_by_owner()
    {
        var path = TempPath();
        SecureFile.WriteAllText(path, "secret-ish");
        Assert.Equal("secret-ish", File.ReadAllText(path));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void WriteAllText_creates_directory_traversable_only_by_owner()
    {
        var path = TempPath();
        SecureFile.WriteAllText(path, "x");
        var dir = Path.GetDirectoryName(path)!;
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(dir));
    }

    [Fact]
    public void WriteAllText_overwrites_existing_file_and_keeps_600()
    {
        var path = TempPath();
        SecureFile.WriteAllText(path, "first");
        SecureFile.WriteAllText(path, "second");
        Assert.Equal("second", File.ReadAllText(path));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void WriteAllLines_writes_lines_with_600()
    {
        var path = TempPath();
        SecureFile.WriteAllLines(path, new[] { "a", "b" });
        Assert.Equal(new[] { "a", "b" }, File.ReadAllLines(path));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void No_temp_files_left_behind()
    {
        var path = TempPath();
        SecureFile.WriteAllText(path, "x");
        var dir = Path.GetDirectoryName(path)!;
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
    }

    [Fact]
    public void WriteAllText_tightens_existing_directory_left_over_from_older_versions()
    {
        var path = TempPath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.SetUnixFileMode(
            dir,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        SecureFile.WriteAllText(path, "x");

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(dir));
    }

    [Fact]
    public void WriteAllText_does_not_touch_shared_sticky_directory()
    {
        var path = TempPath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var shared =
            UnixFileMode.StickyBit |
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(dir, shared);

        SecureFile.WriteAllText(path, "x");

        Assert.Equal(shared, File.GetUnixFileMode(dir));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }
}
