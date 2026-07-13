namespace Vantah.Core.Config;

/// <summary>Пути Vantah в домашней директории пользователя.</summary>
public static class VantahPaths
{
    /// <summary>Директория конфигурации — ~/.config/vantah.</summary>
    public static string ConfigDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "vantah");

    /// <summary>INI-файл конфигурации — ~/.config/vantah/vantah.conf.</summary>
    public static string ConfigFile { get; } = Path.Combine(ConfigDir, "vantah.conf");

    /// <summary>Директория данных — $XDG_DATA_HOME/vantah или ~/.local/share/vantah.</summary>
    public static string DataDir { get; } = Path.Combine(XdgDataHome(), "vantah");

    private static string XdgDataHome()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(dataHome)) return dataHome;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share");
    }
}
