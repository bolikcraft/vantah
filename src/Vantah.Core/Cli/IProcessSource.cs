namespace Vantah.Core.Cli;

/// <summary>Источник живых процессов CLI в системе (а не только тех, что породили мы сами).</summary>
public interface IProcessSource
{
    /// <summary>Снимок процессов CLI на данный момент. Не бросает: гонки со смертью процессов — норма.</summary>
    IReadOnlyList<RunningProcess> Scan();
}
