using Vantah.Core.Parsing;
using Xunit;

public class StatusParserTests
{
    [Fact]
    public void Parses_connected_line_with_ansi()
    {
        var raw = "Connected to [1mAMSTERDAM[0m in [1mTUN[0m mode, running on [1mtun0[0m";
        var s = StatusParser.Parse(raw);
        Assert.True(s.IsConnected);
        Assert.Equal("AMSTERDAM", s.Location);
        Assert.Equal("TUN", s.Mode);
        Assert.Equal("tun0", s.Interface);
        Assert.Null(s.Endpoint);
        Assert.Null(s.SocksPort);
    }

    [Fact]
    public void Connected_fixture_is_recognized()
    {
        var raw = File.ReadAllText("fixtures/status-connected.txt");
        Assert.True(StatusParser.Parse(raw).IsConnected);
    }

    [Fact]
    public void Disconnected_fixture_is_not_connected()
    {
        var raw = File.ReadAllText("fixtures/status-disconnected.txt");
        Assert.False(StatusParser.Parse(raw).IsConnected);
    }

    [Fact]
    public void Recognizes_starting_state()
    {
        // С kill switch connect уходит с флагом --boot и возвращается мгновенно: туннель
        // ещё поднимается, и `status` несколько секунд отдаёт «VPN is starting».
        // Escape-код нарочно разрывает саму фразу: искать надо в очищенном тексте.
        var raw = "[1mVPN is st[0marting[0m\n" +
                  "You can disconnect by running `adguardvpn-cli disconnect`\n";
        var s = StatusParser.Parse(raw);
        Assert.True(s.IsStarting);
        Assert.False(s.IsConnected);
    }

    [Fact]
    public void Recognizes_starting_state_regardless_of_case()
    {
        Assert.True(StatusParser.Parse("vpn is STARTING").IsStarting);
    }

    [Fact]
    public void Connected_output_is_not_starting()
    {
        var s = StatusParser.Parse("Connected to AMSTERDAM in TUN mode, running on tun0");
        Assert.True(s.IsConnected);
        Assert.False(s.IsStarting);
    }

    /// <summary>
    /// В режиме SOCKS туннеля нет, и хвост строки другой: не «running on tun0», а
    /// «listening on 127.0.0.1:1080». Раньше регулярка знала только про туннель, и
    /// подключение в этом режиме читалось как «отключено».
    /// </summary>
    [Fact]
    public void Parses_connected_line_in_socks_mode()
    {
        var raw = "Connected to [1mSINGAPORE[0m in [1mSOCKS[0m mode, " +
                  "listening on [1m127.0.0.1:1080[0m";

        var s = StatusParser.Parse(raw);

        Assert.True(s.IsConnected);
        Assert.Equal("SINGAPORE", s.Location);
        Assert.Equal("SOCKS", s.Mode);
        // Адрес прокси нужен, чтобы найти демона и посчитать по его сокетам трафик.
        Assert.Equal("127.0.0.1:1080", s.Endpoint);
        Assert.Equal(1080, s.SocksPort);
        // Сетевого интерфейса у SOCKS нет — считать по нему трафик нечего.
        Assert.Null(s.Interface);
    }

    [Fact]
    public void Connected_socks_fixture_is_recognized()
    {
        var raw = File.ReadAllText("fixtures/status-connected-socks.txt");
        Assert.True(StatusParser.Parse(raw).IsConnected);
    }

    [Fact]
    public void Unrecognized_output_is_disconnected_and_not_starting()
    {
        var s = StatusParser.Parse("some unrelated garbage");
        Assert.False(s.IsConnected);
        Assert.False(s.IsStarting);
    }
}
