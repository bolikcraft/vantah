using System.Diagnostics;
using Vantah.Core.Cli;
using Xunit;

/// <summary>
/// Интеграционные: трогают реальные процессы. По умолчанию no-op — включаются
/// переменной окружения <c>VANTAH_INTEGRATION=1</c>.
/// </summary>
[Trait("Category", "Integration")]
public class PosixProcessKillerTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("VANTAH_INTEGRATION") == "1";

    [Fact]
    public async Task Kills_a_real_process()
    {
        if (!Enabled) return;

        using var proc = Process.Start("sleep", "30")!;
        var killer = new PosixProcessKiller();

        var killed = await killer.KillAsync(proc.Id);

        Assert.True(killed);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await proc.WaitForExitAsync(cts.Token);
        Assert.True(proc.HasExited);
    }

    [Fact]
    public async Task Returns_false_for_unknown_pid()
    {
        if (!Enabled) return;

        var killer = new PosixProcessKiller();
        Assert.False(await killer.KillAsync(2_000_000_000));
    }

    [Fact]
    public async Task External_kill_command_kills_a_real_process()
    {
        if (!Enabled) return;

        using var proc = Process.Start("sleep", "30")!;
        var killer = new PosixProcessKiller("/bin/kill -9");

        var killed = await killer.KillAsync(proc.Id);

        Assert.True(killed);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await proc.WaitForExitAsync(cts.Token);
        Assert.True(proc.HasExited);
    }

    [Fact]
    public async Task External_kill_command_returns_false_when_it_fails()
    {
        if (!Enabled) return;

        // Несуществующий PID: /bin/kill завершится с ненулевым кодом.
        var killer = new PosixProcessKiller("/bin/kill -9");
        Assert.False(await killer.KillAsync(2_000_000_000));
    }

    [Fact]
    public async Task External_kill_command_returns_false_when_binary_is_missing()
    {
        if (!Enabled) return;

        var killer = new PosixProcessKiller("/nonexistent/vantah-kill");
        Assert.False(await killer.KillAsync(1));
    }

    [Fact]
    public async Task Cancellation_never_propagates_out_of_KillAsync()
    {
        if (!Enabled) return;

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        using var target = Process.Start("sleep", "30")!;
        // Обе ветки (kill(2) и внешняя команда) должны вести себя одинаково: не бросать OCE.
        await new PosixProcessKiller().KillAsync(target.Id, cancelled.Token);
        await new PosixProcessKiller("/bin/kill -9").KillAsync(target.Id, cancelled.Token);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await target.WaitForExitAsync(cts.Token);
        Assert.True(target.HasExited);
    }
}
