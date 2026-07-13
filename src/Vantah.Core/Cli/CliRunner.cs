using System.Diagnostics;
using System.Text;
using Vantah.Core.Config;

namespace Vantah.Core.Cli;

public sealed class CliRunner(string executable = CliOptionsResolver.DefaultExecutable) : ICliRunner
{
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

        try
        {
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
                // Убиваем ТОЛЬКО своё дерево. Привилегированный туннель CLI демонизирует через
                // «sudo -b», нашим потомком он не становится и таймаут его не заденет.
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
        }
    }
}
