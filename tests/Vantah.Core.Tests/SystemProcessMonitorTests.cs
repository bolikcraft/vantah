using Vantah.Core.Cli;
using Xunit;

public class SystemProcessMonitorTests
{
    private sealed class FakeSource : IProcessSource
    {
        public List<RunningProcess> Processes { get; } = [];
        public int Scans { get; private set; }

        public IReadOnlyList<RunningProcess> Scan()
        {
            Scans++;
            return Processes.ToArray();
        }
    }

    private sealed class FakeKiller : IProcessKiller
    {
        public List<int> Killed { get; } = [];
        public bool Result { get; init; } = true;

        public Task<bool> KillAsync(int pid, CancellationToken ct = default)
        {
            Killed.Add(pid);
            return Task.FromResult(Result);
        }
    }

    private static RunningProcess Proc(int pid, params string[] args) =>
        new(pid, pid, "adguardvpn-cli", args, DateTimeOffset.UnixEpoch.AddSeconds(pid));

    [Fact]
    public void Snapshot_shows_processes_scanned_at_construction()
    {
        var source = new FakeSource { Processes = { Proc(10, "connect") } };

        var monitor = new SystemProcessMonitor(source, new FakeKiller());

        Assert.Equal([10], monitor.Snapshot().Select(p => p.Pid));
    }

    [Fact]
    public void Refresh_raises_Changed_when_the_set_of_processes_changed()
    {
        var source = new FakeSource { Processes = { Proc(10, "connect") } };
        var monitor = new SystemProcessMonitor(source, new FakeKiller());
        var changed = 0;
        monitor.Changed += (_, _) => changed++;

        source.Processes.Add(Proc(11, "status"));
        monitor.Refresh();

        Assert.Equal(1, changed);
        Assert.Equal([10, 11], monitor.Snapshot().Select(p => p.Pid));
    }

    [Fact]
    public void Refresh_stays_silent_when_the_same_processes_are_still_alive()
    {
        // Иначе вкладка перерисовывалась бы на каждый опрос и теряла выделение/открытый Flyout.
        var source = new FakeSource { Processes = { Proc(10, "connect") } };
        var monitor = new SystemProcessMonitor(source, new FakeKiller());
        var changed = 0;
        monitor.Changed += (_, _) => changed++;

        monitor.Refresh();
        monitor.Refresh();

        Assert.Equal(0, changed);
    }

    [Fact]
    public void Refresh_survives_a_subscriber_that_throws()
    {
        var source = new FakeSource { Processes = { Proc(10) } };
        var monitor = new SystemProcessMonitor(source, new FakeKiller());
        monitor.Changed += (_, _) => throw new InvalidOperationException("кривой подписчик");

        source.Processes.Clear();
        monitor.Refresh();

        Assert.Empty(monitor.Snapshot());
    }

    [Fact]
    public async Task KillAsync_kills_the_pid_of_the_requested_row()
    {
        var killer = new FakeKiller();
        var source = new FakeSource { Processes = { Proc(10), Proc(11) } };
        var monitor = new SystemProcessMonitor(source, killer);

        var killed = await monitor.KillAsync(11);

        Assert.True(killed);
        Assert.Equal([11], killer.Killed);
    }

    [Fact]
    public async Task KillAsync_rescans_so_the_dead_row_disappears()
    {
        var killer = new FakeKiller();
        var source = new FakeSource { Processes = { Proc(10) } };
        var monitor = new SystemProcessMonitor(source, killer);
        source.Processes.Clear(); // убийца сработал — процесса больше нет

        await monitor.KillAsync(10);

        Assert.Empty(monitor.Snapshot());
    }

    [Fact]
    public async Task KillAsync_of_an_unknown_id_kills_nobody()
    {
        // Строка могла устареть: процесс умер сам между опросом и кликом. Бить чужой pid нельзя.
        var killer = new FakeKiller();
        var monitor = new SystemProcessMonitor(new FakeSource(), killer);

        var killed = await monitor.KillAsync(999);

        Assert.False(killed);
        Assert.Empty(killer.Killed);
    }

    [Fact]
    public async Task KillAllAsync_kills_every_process_in_the_snapshot()
    {
        var killer = new FakeKiller();
        var source = new FakeSource { Processes = { Proc(10), Proc(11), Proc(12) } };
        var monitor = new SystemProcessMonitor(source, killer);

        await monitor.KillAllAsync();

        Assert.Equal([10, 11, 12], killer.Killed.OrderBy(p => p));
    }
}
