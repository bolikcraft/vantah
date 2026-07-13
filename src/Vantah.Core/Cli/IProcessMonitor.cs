namespace Vantah.Core.Cli;

/// <summary>
/// Фасад монитора процессов CLI для UI: снимок живых процессов, уведомление об изменениях
/// и принудительное убийство строки. Видит ВСЕ процессы CLI в системе, включая туннель,
/// который CLI демонизирует через «sudo -b» и который потомком Vantah не является.
/// </summary>
public interface IProcessMonitor
{
    /// <summary>Иммутабельный снимок живых процессов.</summary>
    IReadOnlyList<RunningProcess> Snapshot();

    /// <summary>Поднимается, когда набор процессов изменился. Может прийти не из UI-потока.</summary>
    event EventHandler? Changed;

    /// <summary>Убивает процесс строки. false — процесса уже нет в снимке или убить не удалось.</summary>
    Task<bool> KillAsync(long id, CancellationToken ct = default);

    /// <summary>Убивает все процессы из текущего снимка.</summary>
    Task KillAllAsync(CancellationToken ct = default);
}
