using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Auth;

public static class LoginStateParser
{
    // Точные маркеры из бинаря adguardvpn-cli v1.7.13 (см. спайк Task 0). Достаточно совпадения по подстроке.
    private static readonly string[] LoggedOutMarkers =
    {
        "please log in",            // license / list-locations / connect
        "you must log in",          // «Before connecting to a location, you must log in by running …»
        "your session has expired", // протухшая сессия
    };

    public static LoginState Parse(string cliOutput)
    {
        if (string.IsNullOrWhiteSpace(cliOutput)) return LoginState.Unknown;
        var text = Ansi.Strip(cliOutput);
        foreach (var m in LoggedOutMarkers)
            if (text.Contains(m, StringComparison.OrdinalIgnoreCase))
                return LoginState.LoggedOut;
        return LoginState.LoggedIn;
    }
}
