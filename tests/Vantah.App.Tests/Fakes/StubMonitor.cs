using Vantah.Core.Cli;

namespace Vantah.App.Tests.Fakes;

/// <summary>Монитор-обманка: отдаёт заданный набор процессов и запоминает, кого просили убить.</summary>
public sealed class StubMonitor(params RunningProcess[] processes) : IProcessMonitor
{
    public List<long> Killed { get; } = [];

    public IReadOnlyList<RunningProcess> Snapshot() => processes;

    public event EventHandler? Changed
    {
        add { } remove { }
    }

    public Task<bool> KillAsync(long id, CancellationToken ct = default)
    {
        Killed.Add(id);
        return Task.FromResult(true);
    }

    public Task KillAllAsync(CancellationToken ct = default)
    {
        Killed.AddRange(processes.Select(p => p.Id));
        return Task.CompletedTask;
    }
}
