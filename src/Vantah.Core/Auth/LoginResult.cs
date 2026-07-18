namespace Vantah.Core.Auth;

public sealed record LoginResult(bool Success, string Message)
{
    public static LoginResult Ok() => new(true, "Вход выполнен");
    public static LoginResult Fail(string reason) => new(false, reason);
}
