using Vantah.Core.Errors;

namespace Vantah.Core.Auth;

/// <summary>Итог входа. <paramref name="Message"/> — код причины: текст соберёт UI на своём языке.</summary>
public sealed record LoginResult(bool Success, AppError Message)
{
    public static LoginResult Ok() => new(true, new AppError(AppErrorCode.LoginSucceeded));
    public static LoginResult Fail(AppErrorCode code) => new(false, new AppError(code));
}
