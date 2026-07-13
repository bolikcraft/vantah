namespace Vantah.Core.Cli;

public interface IProcessKiller
{
    /// <summary>Убивает процесс. true — сигнал/команда убийства поданы успешно.</summary>
    Task<bool> KillAsync(int pid, CancellationToken ct = default);
}
