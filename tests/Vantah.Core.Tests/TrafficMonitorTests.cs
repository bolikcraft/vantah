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

    private sealed class FakeSocksReader : ISocksTrafficReader
    {
        public (long rx, long tx)? Next;
        public int? Port;
        public (long rx, long tx)? Read(int socksPort) { Port = socksPort; return Next; }
    }

    /// <summary>В режиме SOCKS интерфейса нет, счётчики приходят от сокетов демона.</summary>
    [Fact]
    public void Socks_poll_computes_rate_the_same_way()
    {
        var socks = new FakeSocksReader { Next = (1000, 500) };
        var mon = new TrafficMonitor(new FakeReader(), socks);

        mon.PollSocks(1080, 1.0);
        socks.Next = (3000, 1500);
        var s = mon.PollSocks(1080, 2.0)!.Value;

        Assert.Equal(1080, socks.Port);
        Assert.Equal(1000, s.RxBytesPerSec);
        Assert.Equal(500, s.TxBytesPerSec);
    }

    /// <summary>Смена режима на живом счётчике не должна дать всплеск скорости.</summary>
    [Fact]
    public void Switching_between_tunnel_and_socks_resets_baseline()
    {
        var reader = new FakeReader { Next = (100000, 50000) };
        var socks = new FakeSocksReader { Next = (10, 10) };
        var mon = new TrafficMonitor(reader, socks);

        mon.Poll("tun0", 1.0);
        var s = mon.PollSocks(1080, 1.0)!.Value;

        Assert.Equal(0, s.RxBytesPerSec);
        Assert.Equal(0, s.TxBytesPerSec);
    }

    [Fact]
    public void Without_a_socks_reader_there_is_no_sample()
    {
        Assert.Null(new TrafficMonitor(new FakeReader()).PollSocks(1080, 1.0));
    }
}
