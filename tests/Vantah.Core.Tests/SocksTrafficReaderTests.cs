using Vantah.Core.Traffic;

/// <summary>
/// Накопление трафика в режиме SOCKS. Счётчики живут в сокете, а сокет может смениться
/// (переподключение) — сумма обязана только расти.
/// </summary>
public class SocksTrafficReaderTests
{
    private const string Header = "State  Recv-Q Send-Q Local Address:Port Peer Address:Port Process\n";
    private const string Listener =
        "LISTEN 0 4096 127.0.0.1:1080 0.0.0.0:* users:((\"adguardvpn-cli\",pid=4242,fd=25))\n";

    private static string Tunnel(int port, long sent, long received) =>
        $"ESTAB 0 0 192.168.0.2:{port} 203.0.113.10:443 users:((\"adguardvpn-cli\",pid=4242,fd=80))\n" +
        $"\t cubic rto:253 bytes_sent:{sent} bytes_acked:{sent} bytes_received:{received} segs_out:12\n";

    private static string Output(params string[] sockets) => Header + Listener + string.Concat(sockets);

    [Fact]
    public void The_first_read_takes_the_whole_socket_counter()
    {
        var reader = new SocksTrafficReader(() => Output(Tunnel(37782, 1000, 5000)));

        Assert.Equal((5000L, 1000L), reader.Read(1080));
    }

    [Fact]
    public void Growing_counters_are_followed()
    {
        var output = Output(Tunnel(37782, 1000, 5000));
        var reader = new SocksTrafficReader(() => output);
        reader.Read(1080);

        output = Output(Tunnel(37782, 1500, 9000));

        Assert.Equal((9000L, 1500L), reader.Read(1080));
    }

    /// <summary>
    /// Переподключение: старый сокет исчез, у нового счётчики с нуля. Его байты добавляются
    /// к уже накопленным, а не заменяют их — иначе показания прыгнули бы назад.
    /// </summary>
    [Fact]
    public void A_new_connection_adds_to_the_total_instead_of_resetting_it()
    {
        var output = Output(Tunnel(37782, 1000, 5000));
        var reader = new SocksTrafficReader(() => output);
        reader.Read(1080);

        output = Output(Tunnel(40100, 20, 700));

        Assert.Equal((5700L, 1020L), reader.Read(1080));
    }

    /// <summary>Тишина в соединении — сумма стоит на месте: иначе каждый опрос добавлял бы
    /// счётчик сокета заново, и скорость показывалась бы десятками мегабайт в секунду.</summary>
    [Fact]
    public void Idle_connection_adds_nothing()
    {
        var reader = new SocksTrafficReader(() => Output(Tunnel(37782, 1000, 5000)));
        reader.Read(1080);

        Assert.Equal((5000L, 1000L), reader.Read(1080));
        Assert.Equal((5000L, 1000L), reader.Read(1080));
    }

    [Fact]
    public void A_connection_that_disappeared_keeps_its_bytes_in_the_total()
    {
        var output = Output(Tunnel(37782, 1000, 5000));
        var reader = new SocksTrafficReader(() => output);
        reader.Read(1080);

        output = Output();

        Assert.Equal((5000L, 1000L), reader.Read(1080));
    }

    [Fact]
    public void Without_ss_output_there_is_no_reading()
    {
        Assert.Null(new SocksTrafficReader(() => null).Read(1080));
    }

    [Fact]
    public void Without_a_daemon_on_that_port_there_is_no_reading()
    {
        var reader = new SocksTrafficReader(() => Output(Tunnel(37782, 1000, 5000)));

        Assert.Null(reader.Read(9050));
    }
}
