namespace Vantah.Core.Cli;

/// <summary>Опознание процессов AdGuard VPN CLI по их командной строке.</summary>
public static class ProcessCmdline
{
    /// <summary>
    /// true, если процесс — это CLI. Ищем совпадение по <b>любому</b> токену, а не только по нулевому:
    /// привилегированный туннель CLI запускает через обёртку («sudo -b env … adguardvpn-cli connect»),
    /// и там наш бинарь стоит в середине строки. Сравниваем имя файла целиком, поэтому путь к логу
    /// «/var/log/adguardvpn-cli.log» за процесс CLI не принимается.
    /// </summary>
    public static bool Matches(IReadOnlyList<string> cmdline, string executable)
    {
        var name = Path.GetFileName(executable);
        if (string.IsNullOrEmpty(name)) return false;

        return cmdline.Any(token => Path.GetFileName(token) == name);
    }
}
