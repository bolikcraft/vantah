using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Vantah.Core.Config;

namespace Vantah.Core.Cli;

public sealed class CliRunner(string executable = CliOptionsResolver.DefaultExecutable) : ICliRunner, IInteractiveCliRunner
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

    // BRANCH B (stdin-pipe) — выбрано по итогам спайка Task 0: у `login` нет флагов, вход
    // интерактивный; определить наверняка stdin vs PTY нельзя было без разлогина пользователя.
    // Реализация изолирована за IInteractiveCliRunner: если ручная проверка покажет, что
    // приглашения (`Enter username:`/`Enter password:`) не приходят через обычный pipe
    // (термиос читает пароль с /dev/tty) — заменить эту реализацию на PTY-ветку локально.
    public Task<IInteractiveCliSession> StartAsync(string[] args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var proc = new Process { StartInfo = psi };
        proc.Start();
        return Task.FromResult<IInteractiveCliSession>(new ProcessInteractiveSession(proc));
    }

    /// <summary>
    /// Интерактивная сессия поверх реального процесса (не покрывается юнит-тестами, как и RunAsync).
    /// Читает stdout+stderr посимвольно (приглашения вроде «Enter password: » печатаются без
    /// перевода строки, поэтому построчное чтение зависло бы) и сливает их в один канал.
    /// </summary>
    private sealed class ProcessInteractiveSession : IInteractiveCliSession
    {
        private readonly Process _proc;
        private readonly Channel<string?> _chunks = Channel.CreateUnbounded<string?>();
        private int _readersAlive = 2;

        public ProcessInteractiveSession(Process proc)
        {
            _proc = proc;
            _ = PumpAsync(proc.StandardOutput);
            _ = PumpAsync(proc.StandardError);
        }

        private async Task PumpAsync(StreamReader reader)
        {
            var buffer = new char[1024];
            try
            {
                int n;
                while ((n = await reader.ReadAsync(buffer)) > 0)
                    await _chunks.Writer.WriteAsync(new string(buffer, 0, n));
            }
            catch { /* поток закрылся — обычное завершение процесса */ }
            finally
            {
                // Когда оба потока (stdout+stderr) закрыты — сигнализируем конец null'ом.
                if (Interlocked.Decrement(ref _readersAlive) == 0)
                    _chunks.Writer.TryComplete();
            }
        }

        public async Task<string?> ReadAsync(CancellationToken ct = default)
        {
            try
            {
                return await _chunks.Reader.ReadAsync(ct);
            }
            catch (ChannelClosedException)
            {
                return null;   // оба потока закрыты, дальше вывода не будет
            }
        }

        public async Task WriteLineAsync(string line, CancellationToken ct = default)
        {
            await _proc.StandardInput.WriteLineAsync(line.AsMemory(), ct);
            await _proc.StandardInput.FlushAsync(ct);
        }

        public async Task WriteLineAsync(char[] chars, CancellationToken ct = default)
        {
            // Пишем символы напрямую, не материализуя пароль в string.
            await _proc.StandardInput.WriteAsync(chars.AsMemory(), ct);
            await _proc.StandardInput.WriteLineAsync();
            await _proc.StandardInput.FlushAsync(ct);
        }

        public async Task<int> WaitForExitAsync(CancellationToken ct = default)
        {
            await _proc.WaitForExitAsync(ct);
            return _proc.ExitCode;
        }

        public async ValueTask DisposeAsync()
        {
            try { if (!_proc.HasExited) _proc.Kill(entireProcessTree: true); } catch { /* уже мёртв */ }
            await Task.CompletedTask;
            _proc.Dispose();
        }
    }
}
