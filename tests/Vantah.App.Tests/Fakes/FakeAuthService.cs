using Vantah.Core.Auth;
using Vantah.Core.Models;

namespace Vantah.App.Tests.Fakes;

/// <summary>Тихий дубль авторизации: по умолчанию «залогинен», вход/выход — no-op.</summary>
public sealed class FakeAuthService : IAuthService
{
    public LoginState State { get; set; } = LoginState.LoggedIn;
    public int LogoutCalls { get; private set; }

    public Task<LoginState> GetLoginStateAsync(CancellationToken ct = default) => Task.FromResult(State);

    public Task<LoginResult> LoginAsync(string email, SecureCredential password, Func<string?> twoFactorProvider, CancellationToken ct = default)
    {
        password.Clear();
        return Task.FromResult(LoginResult.Ok());
    }

    public Task LogoutAsync(CancellationToken ct = default) { LogoutCalls++; return Task.CompletedTask; }
}
