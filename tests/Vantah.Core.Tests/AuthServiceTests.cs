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
    public async Task Login_reports_device_code_prompt_and_succeeds_on_exit_zero()
    {
        var cli = new FakeCliRunner();
        var session = new FakeInteractiveSession(
            "You need to authorize in your browser. The following link will be available for 1673 seconds: " +
            "https://host.test/device_code?user_code=FKGB-NNBQ\n",
            "b - Open link in browser\n",
            null) { ExitCode = 0 };
        var auth = NewAuth(cli, session);

        DeviceCodePrompt? seen = null;
        var result = await auth.LoginAsync(p => seen = p);

        Assert.True(result.Success);
        Assert.NotNull(seen);
        Assert.Equal("https://host.test/device_code?user_code=FKGB-NNBQ", seen!.Url);
        Assert.Equal("FKGB-NNBQ", seen.UserCode);
    }

    [Fact]
    public async Task Login_reports_failure_when_process_exits_nonzero_and_not_logged_in()
    {
        var cli = new FakeCliRunner().Enqueue("Please log in to view your license info");   // зонд: не залогинен
        var session = new FakeInteractiveSession(
            "The following link will be available for 60 seconds: https://host.test/d?user_code=AAAA-BBBB\n",
            null) { ExitCode = 1 };
        var auth = NewAuth(cli, session);

        var result = await auth.LoginAsync(_ => { });
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Login_cancellation_reports_cancelled()
    {
        var cli = new FakeCliRunner();
        var session = new FakeInteractiveSession(
            "The following link will be available for 60 seconds: https://host.test/d?user_code=AAAA-BBBB\n");
        // Нет завершающего null → ReadAsync вернёт null сразу (очередь пуста) — эмулируем отмену явно.
        var auth = NewAuth(cli, session);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await auth.LoginAsync(_ => { }, cts.Token);
        Assert.False(result.Success);
        Assert.Contains("отмен", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}
