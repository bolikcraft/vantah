using Vantah.Core.Auth;
using Vantah.Core.Cli;
using Vantah.Core.Models;
using Vantah.Core.Tests.Fakes;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.Core.Tests;

public class AuthServiceTests
{
    private static AuthService NewAuth(FakeCliRunner cli, FakeInteractiveSession? session = null) =>
        new(cli, new FakeInteractiveRunner(session ?? new FakeInteractiveSession()));

    [Fact]
    public async Task GetLoginState_uses_license_probe_and_detects_logged_out()
    {
        var cli = new FakeCliRunner().Enqueue("Please log in to view your license info");
        var auth = NewAuth(cli);
        Assert.Equal(LoginState.LoggedOut, await auth.GetLoginStateAsync());
        Assert.Equal(new[] { "license" }, cli.Calls[0]);
    }

    [Fact]
    public async Task GetLoginState_logged_in_when_license_shows_account()
    {
        var cli = new FakeCliRunner().Enqueue("Logged in as user@example.com");
        var auth = NewAuth(cli);
        Assert.Equal(LoginState.LoggedIn, await auth.GetLoginStateAsync());
    }

    [Fact]
    public async Task GetLoginState_reads_stderr_when_stdout_empty()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "Please log in to view your license info"));
        var auth = NewAuth(cli);
        Assert.Equal(LoginState.LoggedOut, await auth.GetLoginStateAsync());
    }

    [Fact]
    public async Task Logout_runs_logout_command()
    {
        var cli = new FakeCliRunner().Enqueue("You are now logged out. You can log in by running `adguardvpn-cli login`");
        var auth = NewAuth(cli);
        await auth.LogoutAsync();
        Assert.Equal(new[] { "logout" }, cli.Calls[0]);
    }

    [Fact]
    public async Task Logout_failure_throws()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "some error"));
        var auth = NewAuth(cli);
        await Assert.ThrowsAsync<VpnCommandException>(() => auth.LogoutAsync());
    }

    [Fact]
    public async Task Login_delegates_to_sequencer_and_returns_result()
    {
        var cli = new FakeCliRunner();
        var session = new FakeInteractiveSession("Enter username: ", "Enter password: ", "Logged in", null);
        var auth = NewAuth(cli, session);
        var result = await auth.LoginAsync("user@example.com", new SecureCredential("p4ss".ToCharArray()), () => null);
        Assert.True(result.Success);
    }
}
