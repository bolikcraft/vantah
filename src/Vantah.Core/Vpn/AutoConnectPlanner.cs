using Vantah.Core.Models;

namespace Vantah.Core.Vpn;

/// <summary>Что делать с автоконнектом на старте. ShouldConnect=false — ничего не делаем.</summary>
public readonly record struct AutoConnectAction(bool ShouldConnect, string? Location, bool Fastest);

/// <summary>Чистое решение об автоподключении при запуске приложения.</summary>
public static class AutoConnectPlanner
{
    private static readonly AutoConnectAction None = new(false, null, false);

    public static AutoConnectAction Plan(
        ConnectionState conn, LoginState login, AutoConnectMode mode, string? lastLocation)
    {
        if (login != LoginState.LoggedIn) return None;
        if (conn != ConnectionState.Disconnected) return None;
        return mode switch
        {
            AutoConnectMode.Fastest => new(true, null, true),
            AutoConnectMode.LastUsed => string.IsNullOrWhiteSpace(lastLocation)
                ? new(true, null, true)
                : new(true, lastLocation, false),
            _ => None,
        };
    }
}
