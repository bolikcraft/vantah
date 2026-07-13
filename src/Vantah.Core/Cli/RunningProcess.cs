namespace Vantah.Core.Cli;

/// <summary>Запись о живом процессе CLI: <paramref name="Id"/> — идентификатор строки в UI, равен pid.</summary>
public sealed record RunningProcess(
    long Id,
    int Pid,
    string Command,
    IReadOnlyList<string> Args,
    DateTimeOffset StartedAt)
{
    /// <summary>Команда с аргументами через пробел, как её видит пользователь.</summary>
    public string CommandLine =>
        Args.Count == 0 ? Command : $"{Command} {string.Join(' ', Args)}";
}
