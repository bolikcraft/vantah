using Vantah.Core.Traffic;

/// <summary>
/// Разбор `ss -tinpa` для режима SOCKS. В образце вывода: демон (слушает 1080) с соединением
/// к серверу VPN и с петлевым соединением клиента, короткий вызов CLI под тем же именем
/// со своим соединением и посторонний процесс.
/// </summary>
public class SsTrafficParserTests
{
    private static string Fixture() => File.ReadAllText("fixtures/ss-socks.txt");

    [Fact]
    public void The_daemon_is_the_process_listening_on_the_socks_port()
    {
        Assert.Equal(4242, SsTrafficParser.FindDaemonPid(Fixture(), 1080));
    }

    [Fact]
    public void Without_a_listener_on_that_port_there_is_no_daemon()
    {
        Assert.Null(SsTrafficParser.FindDaemonPid(Fixture(), 9050));
    }

    /// <summary>
    /// Считаем только соединения демона наружу: петлевые — это клиенты прокси (их байты уже
    /// прошли по туннелю), соединение короткого вызова CLI и чужие процессы — не наш трафик.
    /// </summary>
    [Fact]
    public void Only_the_daemon_connections_to_the_outside_are_counted()
    {
        var sockets = SsTrafficParser.ParseTunnelSockets(Fixture(), 4242);

        var socket = Assert.Single(sockets);
        Assert.Equal(336745, socket.Sent);
        Assert.Equal(109321035, socket.Received);
    }

    [Fact]
    public void Unparsable_output_gives_nothing_instead_of_throwing()
    {
        Assert.Null(SsTrafficParser.FindDaemonPid("ss: command not found", 1080));
        Assert.Empty(SsTrafficParser.ParseTunnelSockets("ss: command not found", 4242));
    }
}
