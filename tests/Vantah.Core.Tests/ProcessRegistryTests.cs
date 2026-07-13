using Vantah.Core.Cli;
using Xunit;

public class ProcessRegistryTests
{
    private static DateTimeOffset At(int minute) => new(2026, 7, 13, 10, minute, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Register_assigns_monotonic_ids_and_fills_fields()
    {
        var now = At(0);
        var registry = new ProcessRegistry(() => now);

        var first = registry.Register(1234, "adguardvpn-cli", ["connect", "-f", "-y"]);
        now = At(1);
        var second = registry.Register(5678, "adguardvpn-cli", []);

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
        Assert.Equal(1234, first.Pid);
        Assert.Equal("adguardvpn-cli", first.Command);
        Assert.Equal(["connect", "-f", "-y"], first.Args);
        Assert.Equal("adguardvpn-cli connect -f -y", first.CommandLine);
        Assert.Equal("adguardvpn-cli", second.CommandLine);
        Assert.Equal(At(0), first.StartedAt);
        Assert.Equal(At(1), second.StartedAt);
    }

    [Fact]
    public void Snapshot_reflects_register_and_deregister()
    {
        var now = At(0);
        var registry = new ProcessRegistry(() => now);
        Assert.Empty(registry.Snapshot());

        var first = registry.Register(1, "adguardvpn-cli", ["status"]);
        now = At(1);
        registry.Register(2, "adguardvpn-cli", ["list-locations"]);

        Assert.Equal([1L, 2L], registry.Snapshot().Select(p => p.Id));

        registry.Deregister(first.Id);

        var rest = registry.Snapshot();
        Assert.Single(rest);
        Assert.Equal(2, rest[0].Pid);
    }

    [Fact]
    public void Find_returns_entry_or_null()
    {
        var registry = new ProcessRegistry(() => At(0));
        var proc = registry.Register(42, "adguardvpn-cli", ["disconnect"]);

        Assert.Same(proc, registry.Find(proc.Id));
        Assert.Null(registry.Find(999));

        registry.Deregister(proc.Id);
        Assert.Null(registry.Find(proc.Id));
    }

    [Fact]
    public void Changed_fires_on_register_and_successful_deregister_only()
    {
        var registry = new ProcessRegistry(() => At(0));
        var fired = 0;
        registry.Changed += (_, _) => fired++;

        var proc = registry.Register(7, "adguardvpn-cli", ["status"]);
        registry.Deregister(proc.Id);
        registry.Deregister(proc.Id); // повторное удаление — не событие
        registry.Deregister(12345);   // неизвестный id — не событие

        Assert.Equal(2, fired);
    }

    [Fact]
    public void Snapshot_is_immutable_copy()
    {
        var registry = new ProcessRegistry(() => At(0));
        registry.Register(1, "adguardvpn-cli", ["status"]);

        var before = registry.Snapshot();
        registry.Register(2, "adguardvpn-cli", ["connect"]);

        Assert.Single(before);
        Assert.Equal(2, registry.Snapshot().Count);
    }
}
