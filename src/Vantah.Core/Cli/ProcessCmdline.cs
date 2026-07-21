namespace Vantah.Core.Cli;

/// <summary>Опознание процессов AdGuard VPN CLI по их командной строке.</summary>
public static class ProcessCmdline
{
    /// <summary>Обёртки, через которые CLI поднимает привилегированный туннель.</summary>
    private static readonly string[] Wrappers = ["sudo", "env", "sh", "bash", "pkexec", "doas"];

    /// <summary>
    /// true, если процесс — это CLI. Совпадение принимается либо по нулевому токену (прямой запуск),
    /// либо по любому другому — но только если нулевой токен является известной обёрткой запуска:
    /// привилегированный туннель CLI поднимает через «sudo -b env … adguardvpn-cli connect».
    /// Иначе посторонний процесс, которому имя нашего бинаря просто передали аргументом
    /// («vim adguardvpn-cli»), считался бы нашим. Сравниваем имя файла целиком, поэтому путь к логу
    /// «/var/log/adguardvpn-cli.log» за процесс CLI не принимается.
    /// </summary>
    public static bool Matches(IReadOnlyList<string> cmdline, string executable)
    {
        var name = Path.GetFileName(executable);
        if (string.IsNullOrEmpty(name) || cmdline.Count == 0) return false;

        // Прямой запуск: argv[0] — это наш бинарь.
        if (Path.GetFileName(cmdline[0]) == name) return true;

        // Запуск через обёртку: наш бинарь стоит дальше, но argv[0] обязан быть обёрткой.
        if (!Wrappers.Contains(Path.GetFileName(cmdline[0]))) return false;

        return cmdline.Skip(1).Any(token => Path.GetFileName(token) == name);
    }
}
