using Vantah.Core.Autostart;
using Xunit;

public class AutostartServiceTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "autostart");

    private static AutostartService Make(string dir) =>
        new(dir, "/home/u/.local/bin/vantah", "vantah");

    [Fact]
    public void Disabled_by_default()
    {
        Assert.False(Make(TempDir()).IsEnabled());
    }

    [Fact]
    public void Enable_writes_desktop_with_exec_and_icon()
    {
        var dir = TempDir();
        var svc = Make(dir);
        svc.Enable();

        var file = Path.Combine(dir, "vantah.desktop");
        Assert.True(File.Exists(file));
        var text = File.ReadAllText(file);
        Assert.Contains("Exec=\"/home/u/.local/bin/vantah\"", text);
        Assert.Contains("Icon=vantah", text);
        Assert.Contains("X-GNOME-Autostart-enabled=true", text);
        Assert.True(svc.IsEnabled());
    }

    [Fact]
    public void Disable_removes_the_file()
    {
        var dir = TempDir();
        var svc = Make(dir);
        svc.Enable();
        svc.Disable();
        Assert.False(svc.IsEnabled());
    }

    [Fact]
    public void Enable_and_disable_are_idempotent()
    {
        var dir = TempDir();
        var svc = Make(dir);
        svc.Enable();
        svc.Enable();            // не бросает
        Assert.True(svc.IsEnabled());
        svc.Disable();
        svc.Disable();           // не бросает
        Assert.False(svc.IsEnabled());
    }

    [Fact]
    public void Exec_with_spaces_is_double_quoted()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var svc = new AutostartService(dir, "/home/user/My Apps/vantah", "vantah");
        svc.Enable();
        var content = File.ReadAllText(Path.Combine(dir, "vantah.desktop"));
        Assert.Contains("Exec=\"/home/user/My Apps/vantah\"", content);
    }

    // Защита в глубину: перевод строки в значении вырвался бы из своей строки .desktop
    // и добавил произвольные ключи Desktop Entry (второй Exec=, Hidden= и т.п.).
    [Fact]
    public void Exec_and_icon_cannot_inject_extra_desktop_keys()
    {
        var dir = TempDir();
        try
        {
            var svc = new AutostartService(dir, "/usr/bin/vantah\nExec=/usr/bin/evil", "vantah\nHidden=true");
            svc.Enable();
            var lines = File.ReadAllLines(Path.Combine(dir, "vantah.desktop"));

            // Перевод строки вырезан: новых ключей не появилось, Exec остался ровно один.
            Assert.Single(lines, l => l.StartsWith("Exec="));
            Assert.DoesNotContain("Hidden=true", lines);
            Assert.DoesNotContain("Exec=/usr/bin/evil", lines);
            Assert.Contains("Icon=vantahHidden=true", lines);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
}
