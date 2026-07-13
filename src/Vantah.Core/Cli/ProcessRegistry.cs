namespace Vantah.Core.Cli;

/// <summary>
/// Потокобезопасный реестр живых процессов CLI. Чистая бухгалтерия: реальные процессы
/// не запускает и не убивает — только учитывает. Часы инжектируются (тестируемость).
/// </summary>
public sealed class ProcessRegistry(Func<DateTime>? clock = null)
{
    private readonly Func<DateTime> _clock = clock ?? (() => DateTime.Now);
    private readonly Dictionary<long, RunningProcess> _processes = new();
    private readonly object _gate = new();
    private long _nextId;

    /// <summary>Поднимается при регистрации и при успешной дерегистрации. Вызывается вне блокировки.</summary>
    public event EventHandler? Changed;

    public RunningProcess Register(int pid, string command, IReadOnlyList<string> args)
    {
        RunningProcess proc;
        lock (_gate)
        {
            proc = new RunningProcess(++_nextId, pid, command, args.ToArray(), _clock());
            _processes[proc.Id] = proc;
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return proc;
    }

    public void Deregister(long id)
    {
        bool removed;
        lock (_gate) removed = _processes.Remove(id);
        if (removed) Changed?.Invoke(this, EventArgs.Empty);
    }

    public RunningProcess? Find(long id)
    {
        lock (_gate) return _processes.GetValueOrDefault(id);
    }

    /// <summary>Иммутабельная копия реестра, отсортированная по времени старта, затем по id.</summary>
    public IReadOnlyList<RunningProcess> Snapshot()
    {
        lock (_gate)
            return _processes.Values
                .OrderBy(p => p.StartedAt)
                .ThenBy(p => p.Id)
                .ToArray();
    }
}
