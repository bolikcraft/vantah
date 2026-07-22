using Vantah.Core.Cli;

namespace Vantah.Core.Errors;

/// <summary>
/// Что именно сорвалось. Ядро не знает языка интерфейса и поэтому наружу отдаёт код, а не текст:
/// строку на языке пользователя собирает UI (<c>Vantah.App.Localization.UiText</c>).
/// </summary>
public enum AppErrorCode
{
    /// <summary>Текст пришёл от самого adguardvpn-cli — показываем как есть, переводить нечего.</summary>
    CliOutput,
    /// <summary>Команда CLI вернула ненулевой код и ничего не написала. Аргумент — команда.</summary>
    CommandFailed,
    /// <summary>Команда CLI не уложилась в таймаут. Аргумент — команда.</summary>
    Timeout,
    /// <summary>Не удалось добавить домен в исключения. Аргумент — домен.</summary>
    AddDomainFailed,
    /// <summary>Не удалось удалить домен из исключений. Аргумент — домен.</summary>
    RemoveDomainFailed,
    /// <summary>Не удалось переключить режим исключений. Аргумент — целевой режим.</summary>
    ModeSwitchFailed,
    /// <summary>Вход выполнен (используется как «сообщение» успешного <see cref="Auth.LoginResult"/>).</summary>
    LoginSucceeded,
    /// <summary>CLI завершился, но вход так и не выполнен.</summary>
    LoginFailed,
    /// <summary>Вход отменён пользователем.</summary>
    LoginCancelled,
    /// <summary>Истекло время ожидания входа.</summary>
    LoginTimedOut,
}

/// <summary>Причина сбоя: код + подстановка (команда, домен, вывод CLI) для сообщения в UI.</summary>
public sealed record AppError(AppErrorCode Code, string Argument = "")
{
    /// <summary>Сообщение самого CLI — единственный случай, когда текст задаётся ядром дословно.</summary>
    public static AppError Cli(string text) => new(AppErrorCode.CliOutput, text.Trim());

    /// <summary>
    /// Ошибка провалившейся команды: показываем вывод CLI, если он есть, иначе — код
    /// <see cref="AppErrorCode.CommandFailed"/> с именем команды.
    /// </summary>
    public static AppError FromCli(CliResult r, string command)
    {
        var text = new[] { r.Stderr, r.Stdout }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return text is null ? new AppError(AppErrorCode.CommandFailed, command) : Cli(text);
    }

    /// <summary>
    /// Как <see cref="FromCli"/>, но при пустом выводе подставляется <paramref name="fallback"/>
    /// целиком (для случаев вроде «не удалось добавить {домен}»).
    /// </summary>
    public static AppError FromCli(CliResult r, AppError fallback)
    {
        var text = new[] { r.Stderr, r.Stdout }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return text is null ? fallback : Cli(text);
    }

    /// <summary>
    /// Причина любого исключения: у «своих» берём код, у чужих (IO, сеть, .NET) переводить
    /// нечего — несём их текст как есть.
    /// </summary>
    public static AppError From(Exception ex) => ex is IAppErrorException e ? e.Error : Cli(ex.Message);

    /// <summary>Технический текст: логи, тесты, <c>Exception.Message</c>. НЕ для показа пользователю.</summary>
    public override string ToString() =>
        Code == AppErrorCode.CliOutput ? Argument
        : Argument.Length == 0 ? Code.ToString()
        : $"{Code}: {Argument}";
}

/// <summary>Исключение с переводимой причиной: UI берёт <see cref="Error"/>, а не <c>Message</c>.</summary>
public interface IAppErrorException
{
    AppError Error { get; }
}
