using System.Diagnostics;
using Vantah.Core.Cli;
using Xunit;

namespace Vantah.Core.Tests;

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
        var entry = Assert.Single(runner.Snapshot());
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

    // Процессов не запускает — env-гейт не нужен, работает всегда.
    [Fact]
    public async Task KillAsync_returns_false_for_unknown_id()
    {
        var runner = new CliRunner("sleep");
        Assert.False(await runner.KillAsync(42));
    }

    /// <summary>
    /// Бросок подписчика на регистрации не должен ломать RunAsync: иначе исключение улетает
    /// из Register до входа в try — запись навсегда остаётся в реестре, а процесс сиротеет.
    /// </summary>
    [Fact]
    public async Task Throwing_Changed_handler_on_register_neither_breaks_RunAsync_nor_leaks()
    {
        if (!Enabled) return;

        var runner = new CliRunner("sleep");
        runner.Changed += (_, _) => throw new InvalidOperationException("handler boom");

        var result = await runner.RunAsync(["1"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(runner.Snapshot());
    }

    /// <summary>
    /// Бросок подписчика на дерегистрации не должен подменять собой настоящее исключение RunAsync:
    /// иначе он вылетает из finally и затирает TimeoutException.
    /// </summary>
    [Fact]
    public async Task Throwing_Changed_handler_on_deregister_does_not_mask_timeout()
    {
        if (!Enabled) return;

        var runner = new CliRunner("sleep");
        var pid = 0;
        runner.Changed += (_, _) =>
        {
            if (runner.Snapshot() is [var p]) pid = p.Pid;
            throw new InvalidOperationException("handler boom");
        };

        await Assert.ThrowsAsync<TimeoutException>(
            () => runner.RunAsync(["30"], TimeSpan.FromMilliseconds(200)));

        Assert.Empty(runner.Snapshot());
        Assert.True(pid > 0);
        await WaitForAsync(() => !IsAlive(pid)); // процесс не осиротел
    }

    /// <summary>Отмену вызывающего RunAsync тоже не должен подменять бросок подписчика.</summary>
    [Fact]
    public async Task Throwing_Changed_handler_on_deregister_does_not_mask_cancellation()
    {
        if (!Enabled) return;

        var runner = new CliRunner("sleep");
        runner.Changed += (_, _) => throw new InvalidOperationException("handler boom");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(["30"], ct: cts.Token));

        Assert.Empty(runner.Snapshot());
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
