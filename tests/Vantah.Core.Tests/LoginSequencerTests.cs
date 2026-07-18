using Vantah.Core.Auth;
using Vantah.Core.Tests.Fakes;
using Xunit;

namespace Vantah.Core.Tests;

public class LoginSequencerTests
{
    private static LoginSequencer NewSequencer(FakeInteractiveSession session, out FakeInteractiveRunner runner)
    {
        runner = new FakeInteractiveRunner(session);
        return new LoginSequencer(runner);
    }

    [Fact]
    public async Task Sends_email_then_password_and_reports_success()
    {
        // Реальные приглашения бинаря: «Enter username: » → «Enter password: » → подтверждение.
        var session = new FakeInteractiveSession(
            "Enter username: ",
            "Enter password: ",
            "Logged in as user@example.com",
            null) { ExitCode = 0 };
        var seq = NewSequencer(session, out var runner);

        var result = await seq.LoginAsync(
            "user@example.com",
            new SecureCredential("p4ss".ToCharArray()),
            twoFactorProvider: () => throw new Xunit.Sdk.XunitException("2FA не должен запрашиваться"));

        Assert.True(result.Success);
        Assert.Equal(new[] { "login" }, runner.StartedArgs);
        Assert.Equal("user@example.com", session.Written[0]);  // email/логин
        Assert.Equal("p4ss", session.Written[1]);              // пароль (в тесте фиктивный)
    }

    [Fact]
    public async Task Wrong_password_reports_failure_without_leaking_input()
    {
        var session = new FakeInteractiveSession(
            "Enter username: ", "Enter password: ", "Incorrect password", null) { ExitCode = 1 };
        var seq = NewSequencer(session, out _);

        var result = await seq.LoginAsync("user@example.com", new SecureCredential("badsecret".ToCharArray()), () => "");

        Assert.False(result.Success);
        Assert.Contains("парол", result.Message, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("badsecret", result.Message);    // введённый пароль не утёк в сообщение
    }

    [Fact]
    public async Task Prompts_two_factor_when_asked()
    {
        var session = new FakeInteractiveSession(
            "Enter username: ", "Enter password: ",
            "To log in, please enter the code from your authenticator app", "Logged in", null) { ExitCode = 0 };
        var seq = NewSequencer(session, out _);
        var asked = false;

        var result = await seq.LoginAsync("user@example.com", new SecureCredential("p4ss".ToCharArray()),
            twoFactorProvider: () => { asked = true; return "123456"; });

        Assert.True(result.Success);
        Assert.True(asked);
        Assert.Contains("123456", session.Written);            // код 2FA отправлен
    }

    [Fact]
    public async Task Email_code_on_unusual_login_is_prompted()
    {
        var session = new FakeInteractiveSession(
            "Enter username: ", "Enter password: ",
            "This is an unusual login. To verify it's your account, please enter the code sent to your email",
            "Logged in", null) { ExitCode = 0 };
        var seq = NewSequencer(session, out _);

        var result = await seq.LoginAsync("user@example.com", new SecureCredential("p4ss".ToCharArray()),
            twoFactorProvider: () => "777888");

        Assert.True(result.Success);
        Assert.Contains("777888", session.Written);
    }

    [Fact]
    public async Task Invalid_email_reports_failure()
    {
        var session = new FakeInteractiveSession("Enter username: ", "Invalid email address", null) { ExitCode = 1 };
        var seq = NewSequencer(session, out _);
        var result = await seq.LoginAsync("nope", new SecureCredential("p".ToCharArray()), () => "");
        Assert.False(result.Success);
        Assert.Contains("email", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}
