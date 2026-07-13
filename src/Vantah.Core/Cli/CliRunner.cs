using System.Diagnostics;
using System.Text;
using Vantah.Core.Config;

namespace Vantah.Core.Cli;

public sealed class CliRunner(
    string executable = CliOptionsResolver.DefaultExecutable,
    IProcessKiller? killer = null,
    ProcessRegistry? registry = null) : ICliRunner, IProcessMonitor
{
    private readonly IProcessKiller _killer = killer ?? new PosixProcessKiller();
    private readonly ProcessRegistry _registry = registry ?? new ProcessRegistry();

    /// <inheritdoc cref="IProcessMonitor.Changed" />
    public event EventHandler? Changed
    {
        add => _registry.Changed += value;
        remove => _registry.Changed -= value;
    }

    public async Task<CliResult> RunAsync(string[] args, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        proc.Start();

        // Регистрация — уже внутри try: что бы ни бросило между стартом и ней, finally успеет
        // убить/дерегистрировать процесс, а не осиротит его.
        RunningProcess? entry = null;
        try
        {
            // Регистрируем сразу после старта: PID уже валиден, а процесс может жить долго.
            entry = _registry.Register(proc.Id, executable, args);

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (timeout is { } t) cts.CancelAfter(t);
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* уже мёртв */ }
                // Если отмену запросил вызывающий — показываем настоящую OperationCanceledException,
                // а не маскируем её таймаутом (linked cts срабатывает и на ct, и на timeout).
                ct.ThrowIfCancellationRequested();
                throw new TimeoutException($"{executable} {string.Join(' ', args)} превысил таймаут");
            }

            return new CliResult(proc.ExitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            // Подстраховка от осиротевших процессов: на штатных путях процесс уже мёртв
            // (успешно завершился либо убит в catch выше), так что это ловит лишь
            // неожиданный бросок между стартом и ожиданием выхода.
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* уже мёртв */ }

            // Дерегистрируем при любом исходе: успех, таймаут, отмена, ошибка.
            if (entry is not null) _registry.Deregister(entry.Id);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RunningProcess> Snapshot() => _registry.Snapshot();

    /// <inheritdoc />
    public async Task<bool> KillAsync(long id, CancellationToken ct = default)
    {
        var entry = _registry.Find(id);
        if (entry is null) return false;

        // Из реестра запись уберёт finally в RunAsync, когда процесс реально умрёт.
        return await _killer.KillAsync(entry.Pid, ct);
    }

    /// <inheritdoc />
    public async Task KillAllAsync(CancellationToken ct = default)
    {
        // Записи независимы — бьём параллельно, иначе N процессов = N × grace-таймаут убийцы.
        // ct проверяем сами: IProcessKiller по контракту отмену наружу не пробрасывает.
        ct.ThrowIfCancellationRequested();
        await Task.WhenAll(_registry.Snapshot().Select(entry => KillAsync(entry.Id, ct)));
    }
}
