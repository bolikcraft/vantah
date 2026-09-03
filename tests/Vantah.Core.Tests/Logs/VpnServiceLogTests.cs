using Vantah.Core.Tests.Fakes;
using Vantah.Core.Vpn;
using Xunit;

/// <summary>
/// Лог разбора `status`: сырая строка CLI рядом с тем, что из неё получилось. Опрос идёт
/// каждые 4 с, поэтому повторы не пишем.
/// </summary>
public class VpnServiceLogTests
{
    [Fact]
    public async Task Logs_raw_line_and_parsed_values()
    {
        var log = new FakeAppLog();
        var cli = new FakeCliRunner()
            .Enqueue("Connection lost. Waiting to reconnect to SINGAPORE in SOCKS mode");

        await new VpnService(cli, log).GetStatusAsync();

        Assert.Equal(
            "status: \"Connection lost. Waiting to reconnect to SINGAPORE in SOCKS mode\""
            + " → Reconnecting, SINGAPORE, SOCKS",
            Assert.Single(log.Lines));
    }

    [Fact]
    public async Task Logs_interface_of_a_connected_tunnel()
    {
        var log = new FakeAppLog();
        var cli = new FakeCliRunner().Enqueue("Connected to AMSTERDAM in TUN mode, running on tun0");

        await new VpnService(cli, log).GetStatusAsync();

        Assert.Equal(
            "status: \"Connected to AMSTERDAM in TUN mode, running on tun0\" → Connected, AMSTERDAM, TUN, tun0",
            Assert.Single(log.Lines));
    }

    [Fact]
    public async Task Repeated_answer_is_written_once()
    {
        var log = new FakeAppLog();
        var cli = new FakeCliRunner()
            .Enqueue("Connected to AMSTERDAM in TUN mode, running on tun0")
            .Enqueue("Connected to AMSTERDAM in TUN mode, running on tun0");
        var svc = new VpnService(cli, log);

        await svc.GetStatusAsync();
        await svc.GetStatusAsync();

        Assert.Single(log.Lines);
    }

    [Fact]
    public async Task Changed_answer_adds_a_line()
    {
        var log = new FakeAppLog();
        var cli = new FakeCliRunner()
            .Enqueue("Connected to AMSTERDAM in TUN mode, running on tun0")
            .Enqueue("VPN is disconnected");
        var svc = new VpnService(cli, log);

        await svc.GetStatusAsync();
        await svc.GetStatusAsync();

        Assert.Equal(2, log.Lines.Count);
        Assert.Equal("status: \"VPN is disconnected\" → Disconnected", log.Lines[1]);
    }

    [Fact]
    public async Task Strips_ansi_and_takes_the_first_non_empty_line()
    {
        var log = new FakeAppLog();
        var cli = new FakeCliRunner().Enqueue(
            "\n[1mConnected to OSLO in TUN mode, running on tun0[0m\nYou can disconnect by running x\n");

        await new VpnService(cli, log).GetStatusAsync();

        Assert.Equal(
            "status: \"Connected to OSLO in TUN mode, running on tun0\" → Connected, OSLO, TUN, tun0",
            Assert.Single(log.Lines));
    }

    [Fact]
    public async Task Disabled_log_stays_empty()
    {
        var log = new FakeAppLog { Enabled = false };
        var cli = new FakeCliRunner().Enqueue("Connected to AMSTERDAM in TUN mode, running on tun0");

        await new VpnService(cli, log).GetStatusAsync();

        Assert.Empty(log.Lines);
    }
}
