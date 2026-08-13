namespace Vantah.Core.Config;

/// <summary>Пути Vantah в домашней директории пользователя.</summary>
public static class VantahPaths
{
    /// <summary>Корень конфигов freedesktop — $XDG_CONFIG_HOME или ~/.config. Может ещё не существовать.</summary>
    public static string ConfigHome { get; } = XdgHome("XDG_CONFIG_HOME", ".config");

    /// <summary>Корень данных freedesktop — $XDG_DATA_HOME или ~/.local/share. Может ещё не существовать.</summary>
    public static string DataHome { get; } = XdgHome("XDG_DATA_HOME", Path.Combine(".local", "share"));

    /// <summary>Директория конфигурации — ~/.config/vantah.</summary>
    public static string ConfigDir { get; } = Path.Combine(ConfigHome, "vantah");

    /// <summary>INI-файл конфигурации — ~/.config/vantah/vantah.conf.</summary>
    public static string ConfigFile { get; } = Path.Combine(ConfigDir, "vantah.conf");

    /// <summary>Директория автозапуска freedesktop — ~/.config/autostart (файлы всех приложений).</summary>
    public static string AutostartDir { get; } = Path.Combine(ConfigHome, "autostart");

    /// <summary>Директория данных — ~/.local/share/vantah.</summary>
    public static string DataDir { get; } = Path.Combine(DataHome, "vantah");

    private static string XdgHome(string variable, string relativeToHome) =>
        Resolve(
            Environment.GetEnvironmentVariable(variable),
            // DoNotVerify обязателен: без него GetFolderPath возвращает ПУСТУЮ строку, если каталога
            // ещё нет (свежий аккаунт без ~/.config). Пустой корень делает путь относительным, и весь
            // конфиг — включая выбранный язык — уезжает в текущий рабочий каталог процесса.
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify),
            relativeToHome);

    /// <summary>Корень XDG: переменная окружения, если задана, иначе путь внутри домашней директории.</summary>
    internal static string Resolve(string? xdgHome, string home, string relativeToHome) =>
        string.IsNullOrWhiteSpace(xdgHome) ? Path.Combine(home, relativeToHome) : xdgHome;
}
