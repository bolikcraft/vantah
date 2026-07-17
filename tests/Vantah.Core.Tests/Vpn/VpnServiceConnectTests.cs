using Vantah.Core.Models;
using Vantah.Core.Tests.Fakes;
using Vantah.Core.Vpn;
using Xunit;

public class VpnServiceConnectTests
{
    // connect затем status: два ответа CLI.
    private static FakeCliRunner CliOkThenStatus() =>
        new FakeCliRunner().Enqueue("connected").Enqueue("VPN is disconnected");

    [Fact]
    public async Task Auto_adds_no_ip_flag()
    {
        var cli = CliOkThenStatus();
        await new VpnService(cli).ConnectAsync("Amsterdam", fastest: false, IpVersionPreference.Auto);

        Assert.DoesNotContain("-4", cli.Calls[0]);
        Assert.DoesNotContain("-6", cli.Calls[0]);
    }

    [Fact]
    public async Task IPv4Only_adds_dash_4()
    {
        var cli = CliOkThenStatus();
        await new VpnService(cli).ConnectAsync("Amsterdam", fastest: false, IpVersionPreference.IPv4Only);

        Assert.Contains("-4", cli.Calls[0]);
    }

    [Fact]
    public async Task IPv6Only_adds_dash_6()
    {
        var cli = CliOkThenStatus();
        await new VpnService(cli).ConnectAsync(null, fastest: true, IpVersionPreference.IPv6Only);

        Assert.Contains("-6", cli.Calls[0]);
    }
}
