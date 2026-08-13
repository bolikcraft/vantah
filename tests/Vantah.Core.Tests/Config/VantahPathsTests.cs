using Vantah.Core.Config;

namespace Vantah.Core.Tests.Config;

/// <summary>
/// Корни XDG. Главный случай — свежий аккаунт, где ~/.config ещё не создан: раньше путь получался
/// относительным («vantah»), и весь конфиг вместе с выбранным языком уезжал в текущий рабочий
/// каталог процесса, откуда следующий запуск его уже не находил.
/// </summary>
public class VantahPathsTests
{
    [Fact]
    public void Falls_back_to_home_when_xdg_variable_is_unset() =>
        Assert.Equal("/home/u/.config", VantahPaths.Resolve(null, "/home/u", ".config"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_xdg_variable_counts_as_unset(string xdg) =>
        Assert.Equal("/home/u/.config", VantahPaths.Resolve(xdg, "/home/u", ".config"));

    [Fact]
    public void Xdg_variable_wins_when_set() =>
        Assert.Equal("/run/user/1000/cfg", VantahPaths.Resolve("/run/user/1000/cfg", "/home/u", ".config"));

    [Fact]
    public void All_paths_are_absolute()
    {
        foreach (var path in new[]
                 {
                     VantahPaths.ConfigHome, VantahPaths.DataHome, VantahPaths.ConfigDir,
                     VantahPaths.ConfigFile, VantahPaths.AutostartDir, VantahPaths.DataDir,
                 })
            Assert.True(Path.IsPathRooted(path), $"относительный путь: «{path}»");
    }

    /// <summary>
    /// Каталоги конфигурации и автозапуска лежат в одном корне: автозапуск — общий каталог
    /// freedesktop (~/.config/autostart), а не наша поддиректория.
    /// </summary>
    [Fact]
    public void Config_and_autostart_share_the_xdg_config_root()
    {
        Assert.Equal(Path.Combine(VantahPaths.ConfigHome, "vantah"), VantahPaths.ConfigDir);
        Assert.Equal(Path.Combine(VantahPaths.ConfigHome, "autostart"), VantahPaths.AutostartDir);
        Assert.Equal(Path.Combine(VantahPaths.DataHome, "vantah"), VantahPaths.DataDir);
    }
}
