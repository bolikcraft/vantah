using Vantah.Core.Auth;
using Vantah.Core.Models;

namespace Vantah.App.Tests.Fakes;

/// <summary>Тихий дубль авторизации: по умолчанию «залогинен», вход/выход — no-op.</summary>
public sealed class FakeAuthService : IAuthService
{
    public LoginState State { get; set; } = LoginState.LoggedIn;
    public int LogoutCalls { get; private set; }

    public Task<LoginState> GetLoginStateAsync(CancellationToken ct = default) => Task.FromResult(State);

    public DeviceCodePrompt Prompt { get; set; } = new("https://example.test/device_code?user_code=TEST-CODE", "TEST-CODE", 600);
    public LoginResult NextResult { get; set; } = LoginResult.Ok();

    public Task<LoginResult> LoginAsync(Action<DeviceCodePrompt> onPrompt, CancellationToken ct = default)
    {
        onPrompt(Prompt);
        return Task.FromResult(NextResult);
    }

    public Task LogoutAsync(CancellationToken ct = default) { LogoutCalls++; return Task.CompletedTask; }
}
