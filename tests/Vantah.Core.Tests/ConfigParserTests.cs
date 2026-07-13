using Vantah.Core.Models;
using Vantah.Core.Settings;
using Xunit;

public class ConfigParserTests
{
    // Формат `config show`: TAB-отступы, ANSI-раскраска, значения вида «Default (auto)».
    private const string Sample =
        "Current configuration:\n" +
        "\tData directory: /home/user/.local/share/adguardvpn-cli\n" +
        "\tMode: tun\n" +
        "\tSOCKS port: 1080\n" +
        "\tSOCKS host: 127.0.0.1\n" +
        "\tSOCKS username: \n" +
        "\tDNS upstream: Default (provided by AdGuard VPN)\n" +
        "\tTunnel routing mode: auto\n" +
        "\tChange system DNS: off\n" +
        "\tCrash reporting: on\n" +
        "\tSend anonymized usage data: on\n" +
        "\tUpdate channel: RELEASE\n" +
        "\tProtocol: \x1b[1mDefault (auto)\x1b[0m\n" +
        "\tPost-quantum cryptography: Default (on)\n" +
        "\tShow hints: Default (on)\n" +
        "\tDebug logging: off\n" +
        "\tShow notifications: Default (off)\n" +
        "\tOutbound interface override: not set\n";

    [Fact]
    public void Parses_all_fields_from_sample()
    {
        var c = ConfigParser.Parse(Sample);

        Assert.Equal("/home/user/.local/share/adguardvpn-cli", c.DataDirectory);
        Assert.Equal(VpnMode.Tun, c.Mode);
        Assert.Equal(1080, c.SocksPort);
        Assert.Equal("127.0.0.1", c.SocksHost);
        Assert.Null(c.SocksUsername);                         // пустое значение → null
        Assert.True(c.DnsIsDefault);
        Assert.Equal(TunnelRoutingMode.Auto, c.TunnelRoutingMode);
        Assert.False(c.ChangeSystemDns);
        Assert.True(c.CrashReporting);
        Assert.True(c.SendAnonymizedUsageData);
        Assert.Equal(UpdateChannel.Release, c.UpdateChannel);  // "RELEASE" → Release
        Assert.Equal(VpnProtocol.Auto, c.Protocol);            // ANSI + "Default (auto)" → Auto
        Assert.True(c.PostQuantum);                            // "Default (on)" → true
        Assert.True(c.ShowHints);
        Assert.False(c.DebugLogging);
        Assert.False(c.ShowNotifications);                     // "Default (off)" → false
        Assert.Null(c.OutboundInterfaceOverride);              // "not set" → null
    }

    [Fact]
    public void Parses_socks_mode_and_custom_values()
    {
        var raw =
            "\tMode: socks\n" +
            "\tSOCKS port: 8899\n" +
            "\tSOCKS username: alice\n" +
            "\tDNS upstream: 94.140.14.14\n" +
            "\tProtocol: quic\n" +
            "\tUpdate channel: BETA\n" +
            "\tTunnel routing mode: script\n" +
            "\tDebug logging: on\n" +
            "\tOutbound interface override: eth0\n";

        var c = ConfigParser.Parse(raw);

        Assert.Equal(VpnMode.Socks, c.Mode);
        Assert.Equal(8899, c.SocksPort);
        Assert.Equal("alice", c.SocksUsername);
        Assert.False(c.DnsIsDefault);
        Assert.Equal("94.140.14.14", c.DnsUpstream);
        Assert.Equal(VpnProtocol.Quic, c.Protocol);
        Assert.Equal(UpdateChannel.Beta, c.UpdateChannel);
        Assert.Equal(TunnelRoutingMode.Script, c.TunnelRoutingMode);
        Assert.True(c.DebugLogging);
        Assert.Equal("eth0", c.OutboundInterfaceOverride);
    }

    // DNS-апстрим — единственное значение с двоеточиями внутри (DoH-URL): режем строку
    // по ПЕРВОМУ двоеточию, иначе адрес обрежется до «https».
    [Fact]
    public void Keeps_colons_inside_value()
    {
        var c = ConfigParser.Parse("\tDNS upstream: https://dns.adguard-dns.com/dns-query\n");

        Assert.Equal("https://dns.adguard-dns.com/dns-query", c.DnsUpstream);
        Assert.False(c.DnsIsDefault);
    }

    [Fact]
    public void Real_fixture_parses_without_throwing()
    {
        var raw = File.ReadAllText("fixtures/config-show.txt");

        var c = ConfigParser.Parse(raw);

        Assert.True(c.SocksPort > 0);
        Assert.False(string.IsNullOrWhiteSpace(c.DnsUpstream));
        Assert.False(string.IsNullOrWhiteSpace(c.DataDirectory));
    }
}
