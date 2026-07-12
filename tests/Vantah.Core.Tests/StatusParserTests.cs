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
}
