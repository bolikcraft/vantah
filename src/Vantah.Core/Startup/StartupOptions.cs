namespace Vantah.Core.Startup;

/// <summary>
/// Разобранные аргументы командной строки. Неизвестные аргументы игнорируются: в .desktop-файлы
/// и в автозапуск сессии попадает всякое (например, ключи оконного менеджера), и падать из-за
/// них приложение не должно.
/// </summary>
public sealed record StartupOptions(bool Help, bool Version, bool Minimized)
{
    /// <summary>Ключ «стартовать свёрнутым в трей» — он же уходит в Exec= автозапуска.</summary>
    public const string MinimizedFlag = "--minimized";

    /// <summary>Обычный запуск: показать окно.</summary>
    public static StartupOptions Default { get; } = new(false, false, false);

    /// <summary>Справка по ключам. Консольный вывод, не переводится.</summary>
    public const string HelpText = """
        Vantah - desktop client for AdGuard VPN CLI.

        Usage: vantah [options]

        Options:
          --minimized   start hidden, show the tray icon only
          --version     print the version and exit
          -h, --help    print this help and exit
        """;

    public static StartupOptions Parse(IEnumerable<string> args)
    {
        bool help = false, version = false, minimized = false;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "-h" or "--help": help = true; break;
                case "--version": version = true; break;
                case MinimizedFlag: minimized = true; break;
            }
        }
        return new StartupOptions(help, version, minimized);
    }
}
