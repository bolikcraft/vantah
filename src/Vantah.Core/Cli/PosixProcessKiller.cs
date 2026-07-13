using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Vantah.Core.Cli;

/// <summary>
/// POSIX-убийца: SIGTERM, затем SIGKILL, если процесс не умер за <see cref="GraceMs"/>.
/// Ожидание смерти — короткий poll живости, а не глухая пауза: иначе всё окно ожидания
/// ядро может переиспользовать PID и SIGKILL прилетит невиновному процессу.
/// </summary>
/// <param name="killCommand">
/// Необязательный шаблон внешней команды убийства (например «pkexec kill») — запускается
/// вместо kill(2), когда прав текущего пользователя не хватает; PID дописывается последним
/// аргументом, результат = <c>ExitCode == 0</c>. Шаблон разбирается простым разбиением по
/// пробелам: кавычки и пути с пробелами НЕ поддерживаются.
/// </param>
public sealed partial class PosixProcessKiller(string? killCommand = null) : IProcessKiller
{
    private const int Sigterm = 15;
    private const int Sigkill = 9;
    private const int GraceMs = 500;
    private const int PollMs = 20;

    /// <inheritdoc />
    public async Task<bool> KillAsync(int pid, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(killCommand))
            return await RunKillCommandAsync(killCommand, pid, ct);

        if (kill(pid, Sigterm) != 0)
            return false; // процесса нет или нет прав

        // Сигнал подан — дальше отмена лишь укорачивает ожидание, наружу не пробрасывается.
        if (!await WaitForDeathAsync(pid, ct) && IsAlive(pid))
            kill(pid, Sigkill);

        return true;
    }

    /// <summary>Ждёт смерти процесса до <see cref="GraceMs"/>. true — умер; false — время вышло или отмена.</summary>
    private static async Task<bool> WaitForDeathAsync(int pid, CancellationToken ct)
    {
        for (var waited = 0; waited < GraceMs; waited += PollMs)
        {
            if (!IsAlive(pid)) return true;
            try { await Task.Delay(PollMs, ct); }
            catch (OperationCanceledException) { return false; }
        }
        return !IsAlive(pid);
    }

    private static async Task<bool> RunKillCommandAsync(string killCommand, int pid, CancellationToken ct)
    {
        var parts = killCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        var psi = new ProcessStartInfo
        {
            FileName = parts[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in parts.Skip(1)) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add(pid.ToString());

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return false;

            // Пайпы обязательно вычитываем: болтливый pkexec/polkit иначе заблокируется на записи.
            var stdout = proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            await Task.WhenAll(stdout, stderr);

            return proc.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            // Команда уже запущена и доработает сама. Семантика та же, что у kill(2):
            // отмена наружу не пробрасывается, «команда подана» → true.
            return true;
        }
        catch
        {
            return false; // нет бинаря, не хватает прав на запуск и т.п.
        }
    }

    /// <summary>Проба живости: сигнал 0 не шлётся, но проверяются существование процесса и права.</summary>
    private static bool IsAlive(int pid) => kill(pid, 0) == 0;

    [LibraryImport("libc", SetLastError = true)]
    private static partial int kill(int pid, int sig);
}
