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
}
