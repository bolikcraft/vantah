namespace Vantah.Core.Cli;

/// <summary>
/// Фасад монитора процессов CLI для UI: снимок живых процессов, уведомление об изменениях
/// и принудительное убийство по внутреннему id реестра (не по PID — id стабилен для UI).
/// </summary>
public interface IProcessMonitor
{
    /// <summary>Иммутабельный снимок живых процессов.</summary>
    IReadOnlyList<RunningProcess> Snapshot();

    /// <summary>Поднимается при появлении и исчезновении процессов. Может прийти не из UI-потока.</summary>
    event EventHandler? Changed;

    /// <summary>Убивает процесс по id реестра. false — записи нет или убить не удалось.</summary>
    Task<bool> KillAsync(long id, CancellationToken ct = default);

    /// <summary>Убивает все процессы из текущего снимка.</summary>
    Task KillAllAsync(CancellationToken ct = default);
}
