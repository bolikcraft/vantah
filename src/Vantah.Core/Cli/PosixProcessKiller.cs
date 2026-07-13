using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Vantah.Core.Cli;

/// <summary>
/// POSIX-убийца: SIGTERM, через <see cref="GraceMs"/> — SIGKILL, если процесс ещё жив.
/// Опционально вместо kill(2) запускается внешняя команда (например «pkexec kill»),
/// когда прав текущего пользователя не хватает; PID дописывается последним аргументом.
/// </summary>
public sealed partial class PosixProcessKiller(string? killCommand = null) : IProcessKiller
{
    private const int Sigterm = 15;
    private const int Sigkill = 9;
    private const int GraceMs = 500;

    public async Task<bool> KillAsync(int pid, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(killCommand))
            return await RunKillCommandAsync(killCommand, pid, ct);

        if (kill(pid, Sigterm) != 0)
            return false; // процесса нет или нет прав

        try { await Task.Delay(GraceMs, ct); }
        catch (OperationCanceledException) { return true; } // сигнал уже подан

        if (IsAlive(pid)) kill(pid, Sigkill);
        return true;
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
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Проба живости: сигнал 0 не шлётся, но проверяются существование процесса и права.</summary>
    private static bool IsAlive(int pid) => kill(pid, 0) == 0;

    [LibraryImport("libc", SetLastError = true)]
    private static partial int kill(int pid, int sig);
}
