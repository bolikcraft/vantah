using Vantah.Core.Auth;
using Vantah.Core.Models;
using Xunit;

namespace Vantah.Core.Tests;

public class LoginStateParserTests
{
    [Theory]
    [InlineData("fixtures/login-required-license.txt")]
    [InlineData("fixtures/login-required-connect.txt")]
    [InlineData("fixtures/login-required-locations.txt")]
    public void Detects_logged_out_from_fixtures(string path)
    {
        var raw = File.ReadAllText(path);
        Assert.Equal(LoginState.LoggedOut, LoginStateParser.Parse(raw));
    }

    [Fact]
    public void Session_expired_is_logged_out()
    {
        Assert.Equal(LoginState.LoggedOut,
            LoginStateParser.Parse("Your session has expired. Please log in again"));
    }

    [Fact]
    public void Strips_ansi_and_is_case_insensitive()
    {
        Assert.Equal(LoginState.LoggedOut,
            LoginStateParser.Parse("\x1b[1mPLEASE LOG IN\x1b[0m to view your license info"));
    }

    [Fact]
    public void Normal_license_output_is_logged_in()
    {
        Assert.Equal(LoginState.LoggedIn,
            LoginStateParser.Parse("Logged in as user@example.com\nYou are using the PREMIUM version"));
    }

    [Fact]
    public void Empty_output_is_unknown()
    {
        Assert.Equal(LoginState.Unknown, LoginStateParser.Parse(""));
    }

    [Fact]
    public void Unrecognized_output_is_unknown_not_logged_in()
    {
        Assert.Equal(LoginState.Unknown, LoginStateParser.Parse("some unexpected error text"));
        Assert.Equal(LoginState.Unknown, LoginStateParser.Parse(""));
    }
}
