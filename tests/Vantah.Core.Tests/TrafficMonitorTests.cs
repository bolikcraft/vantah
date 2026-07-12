using Vantah.Core.Traffic;
using Xunit;

public class TrafficMonitorTests
{
    private sealed class FakeReader : ITrafficReader
    {
        public (long rx, long tx)? Next;
        public (long rx, long tx)? Read(string iface) => Next;
    }

    [Fact]
    public void First_poll_has_zero_rate_but_absolute_bytes()
    {
        var reader = new FakeReader { Next = (1000, 500) };
        var mon = new TrafficMonitor(reader);
        var s = mon.Poll("tun0", elapsedSeconds: 1.0)!.Value;
        Assert.Equal(1000, s.RxBytes);
        Assert.Equal(500, s.TxBytes);
        Assert.Equal(0, s.RxBytesPerSec);
        Assert.Equal(0, s.TxBytesPerSec);
    }

    [Fact]
    public void Second_poll_computes_rate_from_delta()
    {
        var reader = new FakeReader { Next = (1000, 500) };
        var mon = new TrafficMonitor(reader);
        mon.Poll("tun0", 1.0);
        reader.Next = (3000, 1500);
        var s = mon.Poll("tun0", 2.0)!.Value;
        Assert.Equal(1000, s.RxBytesPerSec); // (3000-1000)/2
        Assert.Equal(500, s.TxBytesPerSec);  // (1500-500)/2
    }

    [Fact]
    public void Switching_interface_resets_baseline()
    {
        var reader = new FakeReader { Next = (1000, 500) };
        var mon = new TrafficMonitor(reader);
        mon.Poll("tun0", 1.0);
        reader.Next = (3000, 1500);
        mon.Poll("tun0", 1.0);
        // Переключение на другой интерфейс — трактуется как первый сэмпл, скорость 0.
        reader.Next = (10, 10);
        var s = mon.Poll("tun1", 1.0)!.Value;
        Assert.Equal(0, s.RxBytesPerSec);
        Assert.Equal(0, s.TxBytesPerSec);
    }

    [Fact]
    public void Missing_interface_returns_null_and_resets()
    {
        var reader = new FakeReader { Next = null };
        var mon = new TrafficMonitor(reader);
        Assert.Null(mon.Poll("tun0", 1.0));
    }
}
