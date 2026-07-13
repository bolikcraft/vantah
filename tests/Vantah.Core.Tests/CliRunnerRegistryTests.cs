using System.Diagnostics;
using Vantah.Core.Cli;
using Xunit;

/// <summary>
/// Интеграционные: запускают реальные процессы (<c>sleep</c>). По умолчанию no-op —
/// включаются переменной окружения <c>VANTAH_INTEGRATION=1</c>.
/// </summary>
[Trait("Category", "Integration")]
public class CliRunnerRegistryTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("VANTAH_INTEGRATION") == "1";

    [Fact]
    public async Task RunAsync_registers_process_while_it_runs_and_cleans_up_after()
    {
        if (!Enabled) return;

        var runner = new CliRunner("sleep");
        var run = runner.RunAsync(["1"]);

        await WaitForAsync(() => runner.Snapshot().Count == 1);
        var snapshot = runner.Snapshot();
        var entry = Assert.Single(snapshot);
        Assert.True(entry.Pid > 0);
        Assert.Equal("sleep 1", entry.CommandLine);

        var result = await run;
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(runner.Snapshot());
    }

    [Fact]
    public async Task KillAsync_kills_running_process_and_registry_empties()
    {
        if (!Enabled) return;

        var runner = new CliRunner("sleep");
        var run = runner.RunAsync(["30"]);

        await WaitForAsync(() => runner.Snapshot().Count == 1);
        var entry = runner.Snapshot()[0];

        Assert.True(await runner.KillAsync(entry.Id));

        var result = await run;
        Assert.NotEqual(0, result.ExitCode); // убит сигналом
        await WaitForAsync(() => runner.Snapshot().Count == 0);
        Assert.Empty(runner.Snapshot());
        Assert.False(IsAlive(entry.Pid));
    }

    [Fact]
    public async Task KillAsync_returns_false_for_unknown_id()
    {
        if (!Enabled) return;

        var runner = new CliRunner("sleep");
        Assert.False(await runner.KillAsync(42));
    }

    /// <summary>Короткий poll условия вместо глухой паузы.</summary>
    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5_000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return;
            await Task.Delay(20);
        }
        Assert.True(condition(), "условие не выполнилось за отведённое время");
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
