using Vantah.Core.Models;
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
        Assert.Equal(VpnPhase.Connected, s.Phase);
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
        Assert.Equal(VpnPhase.Starting, s.Phase);
        Assert.False(s.IsConnected);
    }

    [Fact]
    public void Recognizes_starting_state_regardless_of_case()
    {
        Assert.Equal(VpnPhase.Starting, StatusParser.Parse("vpn is STARTING").Phase);
    }

    [Fact]
    public void Connected_output_is_not_starting()
    {
        var s = StatusParser.Parse("Connected to AMSTERDAM in TUN mode, running on tun0");
        Assert.True(s.IsConnected);
        Assert.Equal(VpnPhase.Connected, s.Phase);
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
        Assert.Equal(VpnPhase.Disconnected, s.Phase);
    }

    /// <summary>
    /// При обрыве kill switch (`connect --boot`) бесконечно ретраит и печатает две формы:
    /// «Connection lost. Waiting to reconnect to …» и «Reconnecting to …». Раньше обе читались
    /// как «отключено»: UI показывал «Отключено» с кнопкой «Подключить», а история рвала
    /// живую сессию. Локация и режим в этих строках известны — их надо сохранять.
    /// </summary>
    [Fact]
    public void Parses_connection_lost_line_in_tun_mode()
    {
        var raw = "Connection lost. Waiting to reconnect to [1mAMSTERDAM[0m in " +
                  "[1mTUN[0m mode, running on [1mtun0[0m";

        var s = StatusParser.Parse(raw);

        Assert.Equal(VpnPhase.Reconnecting, s.Phase);
        Assert.False(s.IsConnected);
        Assert.Equal("AMSTERDAM", s.Location);
        Assert.Equal("TUN", s.Mode);
        Assert.Equal("tun0", s.Interface);
        Assert.Null(s.Endpoint);
    }

    /// <summary>
    /// Живой обрыв 03.09.2026: пока сети нет совсем, CLI печатает не «Connection lost», а
    /// «Network lost. Waiting to connect to …» — эта форма читалась как «отключено», и UI
    /// показывал кнопку «Подключить» поверх работающего kill switch.
    /// </summary>
    [Fact]
    public void Parses_network_lost_line_in_socks_mode()
    {
        var raw = "Network lost. Waiting to connect to [1mSINGAPORE[0m in " +
                  "[1mSOCKS[0m mode, listening on [1m127.0.0.1:1080[0m";

        var s = StatusParser.Parse(raw);

        Assert.Equal(VpnPhase.Reconnecting, s.Phase);
        Assert.False(s.IsConnected);
        Assert.Equal("SINGAPORE", s.Location);
        Assert.Equal("SOCKS", s.Mode);
        Assert.Equal("127.0.0.1:1080", s.Endpoint);
        Assert.Null(s.Interface);
    }

    [Fact]
    public void Parses_network_lost_line_in_tun_mode()
    {
        var s = StatusParser.Parse("Network lost. Waiting to connect to AMSTERDAM in TUN mode, running on tun0");

        Assert.Equal(VpnPhase.Reconnecting, s.Phase);
        Assert.Equal("AMSTERDAM", s.Location);
        Assert.Equal("TUN", s.Mode);
        Assert.Equal("tun0", s.Interface);
    }

    [Fact]
    public void Parses_reconnecting_line_in_tun_mode()
    {
        var raw = "Reconnecting to [1mAMSTERDAM[0m in [1mTUN[0m mode, running on [1mtun0[0m";

        var s = StatusParser.Parse(raw);

        Assert.Equal(VpnPhase.Reconnecting, s.Phase);
        Assert.False(s.IsConnected);
        Assert.Equal("AMSTERDAM", s.Location);
        Assert.Equal("TUN", s.Mode);
        Assert.Equal("tun0", s.Interface);
    }

    [Fact]
    public void Parses_connection_lost_line_in_socks_mode()
    {
        var raw = "Connection lost. Waiting to reconnect to [1mSINGAPORE[0m in " +
                  "[1mSOCKS[0m mode, listening on [1m127.0.0.1:1080[0m";

        var s = StatusParser.Parse(raw);

        Assert.Equal(VpnPhase.Reconnecting, s.Phase);
        Assert.False(s.IsConnected);
        Assert.Equal("SINGAPORE", s.Location);
        Assert.Equal("SOCKS", s.Mode);
        Assert.Equal("127.0.0.1:1080", s.Endpoint);
        Assert.Equal(1080, s.SocksPort);
        Assert.Null(s.Interface);
    }

    [Fact]
    public void Parses_reconnecting_line_in_socks_mode()
    {
        var raw = "Reconnecting to [1mSINGAPORE[0m in [1mSOCKS[0m mode, " +
                  "listening on [1m127.0.0.1:1080[0m";

        var s = StatusParser.Parse(raw);

        Assert.Equal(VpnPhase.Reconnecting, s.Phase);
        Assert.Equal("SINGAPORE", s.Location);
        Assert.Equal("SOCKS", s.Mode);
        Assert.Equal("127.0.0.1:1080", s.Endpoint);
        Assert.Equal(1080, s.SocksPort);
    }

    [Fact]
    public void Parses_connection_lost_line_without_tail()
    {
        var s = StatusParser.Parse("Connection lost. Waiting to reconnect to Amsterdam in TUN mode");

        Assert.Equal(VpnPhase.Reconnecting, s.Phase);
        Assert.Equal("Amsterdam", s.Location);
        Assert.Equal("TUN", s.Mode);
        Assert.Null(s.Interface);
        Assert.Null(s.Endpoint);
    }

    [Fact]
    public void Parses_reconnecting_line_without_tail()
    {
        var s = StatusParser.Parse("Reconnecting to Amsterdam in TUN mode");

        Assert.Equal(VpnPhase.Reconnecting, s.Phase);
        Assert.Equal("Amsterdam", s.Location);
        Assert.Equal("TUN", s.Mode);
        Assert.Null(s.Interface);
        Assert.Null(s.Endpoint);
    }

    [Fact]
    public void Reconnecting_forms_are_recognized_regardless_of_case()
    {
        Assert.Equal(VpnPhase.Reconnecting,
            StatusParser.Parse("RECONNECTING TO AMSTERDAM IN TUN MODE").Phase);
        Assert.Equal(VpnPhase.Reconnecting,
            StatusParser.Parse("connection lost. waiting to reconnect to oslo in tun mode").Phase);
    }

    /// <summary>Формы «подключено» и «переподключаемся» не должны путаться между собой:
    /// они отличаются только началом строки, а хвост у них общий.</summary>
    [Fact]
    public void Connected_and_reconnecting_forms_do_not_match_each_other()
    {
        Assert.Equal(VpnPhase.Connected,
            StatusParser.Parse("Connected to OSLO in TUN mode, running on tun0").Phase);
        Assert.True(StatusParser.Parse("Connected to OSLO in TUN mode, running on tun0").IsConnected);

        Assert.False(StatusParser.Parse("Reconnecting to OSLO in TUN mode, running on tun0").IsConnected);
        Assert.False(StatusParser
            .Parse("Connection lost. Waiting to reconnect to OSLO in TUN mode, running on tun0")
            .IsConnected);
    }
}
