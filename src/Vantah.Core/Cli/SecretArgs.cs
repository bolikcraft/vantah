namespace Vantah.Core.Cli;

/// <summary>
/// Маска для значений, которые CLI принимает позиционным аргументом и которые нельзя писать
/// в лог и показывать в окне процессов: логин и пароль SOCKS, адрес DNS. В частном адресе
/// AdGuard DNS есть ID клиента.
/// </summary>
public static class SecretArgs
{
    /// <summary>Команды, чей следующий аргумент — секрет.</summary>
    private static readonly string[] ValueCommands = ["set-socks-username", "set-socks-password", "set-dns"];

    /// <summary>Аргументы через пробел: значение после секретной команды заменено на «***».</summary>
    public static string Join(IReadOnlyList<string> args) =>
        string.Join(' ', args.Select((a, i) => i > 0 && ValueCommands.Contains(args[i - 1]) ? "***" : a));
}
