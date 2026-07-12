using System.Linq;
using Vantah.Core.Tests.Fakes;
using Vantah.Core.Vpn;
using Xunit;

public class VpnServiceTests
{
    [Fact]
    public async Task Connect_by_location_passes_l_flag_and_confirms_via_status()
    {
        var cli = new FakeCliRunner()
            .Enqueue("")                                                   // connect
            .Enqueue("Connected to AMSTERDAM in TUN mode, running on tun0"); // последующий status
        var svc = new VpnService(cli);

        var status = await svc.ConnectAsync("Amsterdam", fastest: false);

        Assert.Equal(new[] { "connect", "-l", "Amsterdam", "-y" }, cli.Calls[0]);
        Assert.Equal(new[] { "status" }, cli.Calls[1]);
        Assert.True(status.IsConnected);
        Assert.Equal("AMSTERDAM", status.Location);
    }

    [Fact]
    public async Task Connect_fastest_passes_f_flag()
    {
        var cli = new FakeCliRunner().Enqueue("").Enqueue("Connected to OSLO in TUN mode, running on tun0");
        var svc = new VpnService(cli);
        await svc.ConnectAsync(location: null, fastest: true);
        Assert.Equal(new[] { "connect", "-f", "-y" }, cli.Calls[0]);
    }

    [Fact]
    public async Task Connect_failure_throws_with_stderr()
    {
        var cli = new FakeCliRunner().Enqueue(new Vantah.Core.Cli.CliResult(1, "", "no such location"));
        var svc = new VpnService(cli);
        var ex = await Assert.ThrowsAsync<VpnCommandException>(() => svc.ConnectAsync("Nowhere", false));
        Assert.Contains("no such location", ex.Message);
    }

    [Fact]
    public async Task GetLocations_parses_output()
    {
        var cli = new FakeCliRunner().Enqueue("EE    Estonia              Tallinn                        24\n");
        var svc = new VpnService(cli);
        var locs = await svc.GetLocationsAsync();
        Assert.Single(locs);
        Assert.Equal("Tallinn", locs[0].City);
    }
}
