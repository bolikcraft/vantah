using Vantah.Core.Cli;
using Vantah.Core.Models;
using Vantah.Core.Tests.Fakes;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.Core.Tests;

public class VpnServiceKillSwitchTests
{
    // connected-ответ, чтобы ConnectAsync не бросил и дошёл до GetStatusAsync
    private static FakeCliRunner Runner() => new FakeCliRunner()
        .Enqueue(new CliResult(0, "VPN is starting", ""))                                   // connect
        .Enqueue(new CliResult(0, "Connected to AMSTERDAM in TUN mode, running on tun0", "")); // status

    [Fact]
    public async Task Adds_boot_flag_when_kill_switch_on()
    {
        var cli = Runner();
        await new VpnService(cli).ConnectAsync("Amsterdam", fastest: false,
            IpVersionPreference.Auto, killSwitch: true);
        Assert.Contains("--boot", cli.Calls[0]);
    }

    [Fact]
    public async Task No_boot_flag_when_kill_switch_off()
    {
        var cli = Runner();
        await new VpnService(cli).ConnectAsync("Amsterdam", fastest: false,
            IpVersionPreference.Auto, killSwitch: false);
        Assert.DoesNotContain("--boot", cli.Calls[0]);
    }
}
