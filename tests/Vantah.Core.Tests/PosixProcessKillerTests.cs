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
}
