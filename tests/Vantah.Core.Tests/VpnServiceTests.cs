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
    public async Task GetStatus_failure_throws_with_stderr()
    {
        // Транзиентный сбой `status` (демон/сеть моргнули) не должен молча парситься
        // как «отключено» — иначе координатор рвёт активную сессию истории.
        var cli = new FakeCliRunner().Enqueue(new Vantah.Core.Cli.CliResult(1, "", "daemon hiccup"));
        var svc = new VpnService(cli);

        var ex = await Assert.ThrowsAsync<VpnCommandException>(() => svc.GetStatusAsync());
        Assert.Contains("daemon hiccup", ex.Message);
    }

    [Fact]
    public async Task GetLocations_failure_throws_with_stderr()
    {
        var cli = new FakeCliRunner().Enqueue(new Vantah.Core.Cli.CliResult(1, "", "network unreachable"));
        var svc = new VpnService(cli);

        var ex = await Assert.ThrowsAsync<VpnCommandException>(() => svc.GetLocationsAsync());
        Assert.Contains("network unreachable", ex.Message);
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

    [Fact]
    public async Task GetLicense_parses_output()
    {
        var cli = new FakeCliRunner().Enqueue(
            "Logged in as user@example.com\n" +
            "You are using the PREMIUM version\n" +
            "Up to 10 devices simultaneously\n" +
            "Your subscription will be renewed on 2028-07-08\n");
        var svc = new VpnService(cli);

        var license = await svc.GetLicenseAsync();

        Assert.Equal(new[] { "license" }, cli.Calls[0]);
        Assert.Equal("user@example.com", license.Email);
        Assert.Equal("PREMIUM", license.Plan);
        Assert.Equal(10, license.MaxDevices);
        Assert.Equal("2028-07-08", license.RenewalDate);
    }

    [Fact]
    public async Task GetLicense_failure_throws_with_stderr()
    {
        // Иначе парсер вернёт License("", "UNKNOWN", 0, null) и сбой (например, не выполнен
        // вход) выглядел бы в UI как успешно загруженная лицензия.
        var cli = new FakeCliRunner().Enqueue(new Vantah.Core.Cli.CliResult(1, "", "You are not logged in"));
        var svc = new VpnService(cli);

        var ex = await Assert.ThrowsAsync<VpnCommandException>(() => svc.GetLicenseAsync());
        Assert.Contains("You are not logged in", ex.Message);
    }

    [Fact]
    public async Task GetCliVersion_calls_version_flag_and_strips_ansi()
    {
        var cli = new FakeCliRunner().Enqueue("AdGuard VPN CLI [1mv1.7.12[0m\n");
        var svc = new VpnService(cli);

        var version = await svc.GetCliVersionAsync();

        Assert.Equal(new[] { "--version" }, cli.Calls[0]);
        Assert.Equal("AdGuard VPN CLI v1.7.12", version);
    }

    [Fact]
    public async Task GetCliVersion_returns_null_when_command_fails()
    {
        var cli = new FakeCliRunner().Enqueue(new Vantah.Core.Cli.CliResult(127, "", "command not found"));
        var svc = new VpnService(cli);

        Assert.Null(await svc.GetCliVersionAsync());
    }

    [Fact]
    public async Task GetCliVersion_returns_null_when_output_empty()
    {
        var cli = new FakeCliRunner().Enqueue("   \n");
        var svc = new VpnService(cli);

        Assert.Null(await svc.GetCliVersionAsync());
    }
}
