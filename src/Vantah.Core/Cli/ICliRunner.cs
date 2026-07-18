namespace Vantah.Core.Cli;

public interface ICliRunner
{
    Task<CliResult> RunAsync(string[] args, TimeSpan? timeout = null, CancellationToken ct = default);
}

/// <summary>Живой интерактивный процесс: читаем приглашения, пишем ответы построчно.</summary>
public interface IInteractiveCliSession : IAsyncDisposable
{
    /// <summary>Прочитать очередную порцию вывода (stdout+stderr слиты). null — поток закрыт (процесс завершается).</summary>
    Task<string?> ReadAsync(CancellationToken ct = default);

    /// <summary>Отправить строку в процесс (добавляет перевод строки). Перегрузка с char[] — для пароля, буфер занулять у вызывающего.</summary>
    Task WriteLineAsync(string line, CancellationToken ct = default);
    Task WriteLineAsync(char[] chars, CancellationToken ct = default);

    /// <summary>Дождаться завершения и вернуть код возврата.</summary>
    Task<int> WaitForExitAsync(CancellationToken ct = default);
}

public interface IInteractiveCliRunner
{
    Task<IInteractiveCliSession> StartAsync(string[] args, CancellationToken ct = default);
}
