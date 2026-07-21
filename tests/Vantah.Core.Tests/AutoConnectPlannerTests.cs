using Vantah.Core.Models;
using Vantah.Core.Vpn;
using Xunit;

public class AutoConnectPlannerTests
{
    [Fact]
    public void Fastest_when_disconnected_and_logged_in()
    {
        var a = AutoConnectPlanner.Plan(ConnectionState.Disconnected, LoginState.LoggedIn,
            AutoConnectMode.Fastest, lastLocation: null);
        Assert.True(a.ShouldConnect);
        Assert.Null(a.Location);
        Assert.True(a.Fastest);
    }

    [Fact]
    public void Last_used_with_location_connects_to_it()
    {
        var a = AutoConnectPlanner.Plan(ConnectionState.Disconnected, LoginState.LoggedIn,
            AutoConnectMode.LastUsed, "Amsterdam");
        Assert.True(a.ShouldConnect);
        Assert.Equal("Amsterdam", a.Location);
        Assert.False(a.Fastest);
    }

    [Fact]
    public void Last_used_without_location_falls_back_to_fastest()
    {
        var a = AutoConnectPlanner.Plan(ConnectionState.Disconnected, LoginState.LoggedIn,
            AutoConnectMode.LastUsed, lastLocation: null);
        Assert.True(a.ShouldConnect);
        Assert.Null(a.Location);
        Assert.True(a.Fastest);
    }

    [Fact]
    public void Off_never_connects()
    {
        var a = AutoConnectPlanner.Plan(ConnectionState.Disconnected, LoginState.LoggedIn,
            AutoConnectMode.Off, "Amsterdam");
        Assert.False(a.ShouldConnect);
    }

    [Fact]
    public void Already_connected_does_nothing()
    {
        var a = AutoConnectPlanner.Plan(ConnectionState.Connected, LoginState.LoggedIn,
            AutoConnectMode.Fastest, null);
        Assert.False(a.ShouldConnect);
    }

    // До первого опроса статус неизвестен — автоконнект решается уже после него.
    [Fact]
    public void Unknown_state_does_nothing()
    {
        var a = AutoConnectPlanner.Plan(ConnectionState.Unknown, LoginState.LoggedIn,
            AutoConnectMode.Fastest, null);
        Assert.False(a.ShouldConnect);
    }

    [Theory]
    [InlineData(LoginState.LoggedOut)]
    [InlineData(LoginState.Unknown)]
    public void Not_logged_in_does_nothing(LoginState login)
    {
        var a = AutoConnectPlanner.Plan(ConnectionState.Disconnected, login,
            AutoConnectMode.Fastest, null);
        Assert.False(a.ShouldConnect);
    }
}
