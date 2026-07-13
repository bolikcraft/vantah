namespace Vantah.Core.Cli;

public interface IProcessKiller
{
    /// <summary>
    /// Убивает процесс. Возвращает true, если сигнал/команда убийства поданы успешно
    /// (false — процесса нет, не хватает прав или команда убийства завершилась с ошибкой).
    /// </summary>
    /// <remarks>
    /// Отмена НЕ пробрасывается наружу: <paramref name="ct"/> лишь укорачивает ожидание смерти
    /// процесса после того, как сигнал/команда уже поданы. Метод не бросает
    /// <see cref="OperationCanceledException"/> — вызывающий может писать
    /// <c>if (await killer.KillAsync(pid, ct))</c> без try/catch, независимо от того,
    /// используется ли kill(2) или внешняя привилегированная kill-команда.
    /// </remarks>
    Task<bool> KillAsync(int pid, CancellationToken ct = default);
}
