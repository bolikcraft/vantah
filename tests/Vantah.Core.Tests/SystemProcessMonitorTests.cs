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

    /// <summary>Источник со сценарием: первый скан отдаёт одно, все следующие — другое.</summary>
    private sealed class ScriptedSource(IReadOnlyList<RunningProcess> first, IReadOnlyList<RunningProcess> then)
        : IProcessSource
    {
        private bool _used;

        public IReadOnlyList<RunningProcess> Scan()
        {
            if (_used) return then;
            _used = true;
            return first;
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
        // Процесс исчез между опросом и кликом: сверка личности провалится и убийца не вызовется.
        // Проверяем, что Refresh() всё равно происходит — строка уходит и на неподтверждённом пути.
        source.Processes.Clear();

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
    public async Task KillAsync_does_not_signal_when_the_process_vanished_after_the_snapshot()
    {
        // Снимок обновляется по таймеру: процесс мог умереть сам, а pid — уйти чужому.
        var proc = new RunningProcess(1, 4242, "adguardvpn-cli", [], DateTimeOffset.UnixEpoch);
        var killer = new FakeKiller();
        var monitor = new SystemProcessMonitor(new ScriptedSource([proc], []), killer);

        var killed = await monitor.KillAsync(proc.Id);

        Assert.False(killed);
        Assert.Empty(killer.Killed);
    }

    [Fact]
    public async Task KillAsync_does_not_signal_when_the_pid_was_reused_by_another_process()
    {
        var original = new RunningProcess(1, 4242, "adguardvpn-cli", [], DateTimeOffset.UnixEpoch);
        var reused = new RunningProcess(1, 4242, "adguardvpn-cli", [], DateTimeOffset.UnixEpoch.AddHours(5));
        var killer = new FakeKiller();
        var monitor = new SystemProcessMonitor(new ScriptedSource([original], [reused]), killer);

        var killed = await monitor.KillAsync(original.Id);

        Assert.False(killed);
        Assert.Empty(killer.Killed);
    }

    [Fact]
    public async Task KillAsync_signals_when_the_identity_still_matches()
    {
        var proc = new RunningProcess(1, 4242, "adguardvpn-cli", [], DateTimeOffset.UnixEpoch);
        var killer = new FakeKiller();
        var monitor = new SystemProcessMonitor(new ScriptedSource([proc], [proc]), killer);

        Assert.True(await monitor.KillAsync(proc.Id));
        Assert.Equal([4242], killer.Killed);
    }

    [Fact]
    public async Task KillAllAsync_skips_the_rows_whose_identity_no_longer_matches()
    {
        var alive = Proc(10);
        var reused = Proc(11) with { StartedAt = DateTimeOffset.UnixEpoch.AddHours(5) };
        var killer = new FakeKiller();
        var monitor = new SystemProcessMonitor(new ScriptedSource([alive, Proc(11)], [alive, reused]), killer);

        await monitor.KillAllAsync();

        Assert.Equal([10], killer.Killed);
    }

    [Fact]
    public async Task KillAllAsync_confirms_identity_with_a_single_rescan()
    {
        // N процессов не должны стоить N проходов по /proc.
        var source = new FakeSource { Processes = { Proc(10), Proc(11), Proc(12) } };
        var monitor = new SystemProcessMonitor(source, new FakeKiller());
        var before = source.Scans;

        await monitor.KillAllAsync();

        Assert.Equal(2, source.Scans - before); // один на сверку личности + один на Refresh
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
