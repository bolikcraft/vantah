namespace Vantah.Core.Cli;

/// <summary>
/// Монитор процессов CLI поверх системного скана. Держит последний снимок и поднимает
/// <see cref="Changed"/>, только когда набор процессов действительно изменился: опрос идёт по таймеру,
/// и событие на каждый тик перетряхивало бы список в UI на ровном месте.
/// </summary>
public sealed class SystemProcessMonitor : IProcessMonitor, IDisposable
{
    private readonly IProcessSource _source;
    private readonly IProcessKiller _killer;
    private readonly object _gate = new();
    private Timer? _timer;
    private IReadOnlyList<RunningProcess> _snapshot;

    public SystemProcessMonitor(IProcessSource source, IProcessKiller killer)
    {
        _source = source;
        _killer = killer;
        _snapshot = source.Scan(); // вкладка должна быть заполнена сразу, а не через такт таймера
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Запускает периодический опрос. Вызывать один раз: повторный вызов заменит таймер.</summary>
    public void StartPolling(TimeSpan interval)
    {
        var timer = new Timer(_ => Refresh(), null, interval, interval);
        Interlocked.Exchange(ref _timer, timer)?.Dispose();
    }

    /// <summary>Пересканировать систему немедленно.</summary>
    public void Refresh()
    {
        var scanned = _source.Scan();

        lock (_gate)
        {
            if (SameProcesses(_snapshot, scanned)) return;
            _snapshot = scanned;
        }

        RaiseChanged();
    }

    /// <inheritdoc />
    public IReadOnlyList<RunningProcess> Snapshot()
    {
        lock (_gate) return _snapshot;
    }

    /// <inheritdoc />
    public async Task<bool> KillAsync(long id, CancellationToken ct = default)
    {
        // Бьём только то, что видели в снимке: строка могла устареть, а ядро — переиспользовать pid
        // под чужой процесс. Без этой проверки Kill по мёртвой строке прилетал бы невиновному.
        var target = Snapshot().FirstOrDefault(p => p.Id == id);
        if (target is null) return false;

        var killed = await _killer.KillAsync(target.Pid, ct);
        Refresh(); // строка убитого процесса должна исчезнуть сразу, не дожидаясь такта таймера
        return killed;
    }

    /// <inheritdoc />
    public async Task KillAllAsync(CancellationToken ct = default)
    {
        // Записи независимы — бьём параллельно, иначе N процессов = N × grace-таймаут убийцы.
        ct.ThrowIfCancellationRequested();
        await Task.WhenAll(Snapshot().Select(p => KillAsync(p.Id, ct)));
    }

    public void Dispose() => Interlocked.Exchange(ref _timer, null)?.Dispose();

    /// <summary>Один и тот же процесс = тот же pid и то же время старта (pid ядро переиспользует).</summary>
    private static bool SameProcesses(IReadOnlyList<RunningProcess> a, IReadOnlyList<RunningProcess> b) =>
        a.Count == b.Count && !a.Where((p, i) => p.Pid != b[i].Pid || p.StartedAt != b[i].StartedAt).Any();

    /// <summary>
    /// Каждый подписчик вызывается отдельно, его исключение проглатывается: монитор — наблюдатель,
    /// он не вправе ронять опрос из-за кривого хендлера в UI.
    /// </summary>
    private void RaiseChanged()
    {
        if (Changed is not { } handlers) return;

        foreach (var handler in handlers.GetInvocationList())
        {
            try { ((EventHandler)handler).Invoke(this, EventArgs.Empty); }
            catch { /* подписчик сам себе злобный буратино */ }
        }
    }
}
