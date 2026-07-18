using Vantah.Core.Models;

namespace Vantah.Core.Auth;

public interface IAuthService
{
    Task<LoginState> GetLoginStateAsync(CancellationToken ct = default);
    Task<LoginResult> LoginAsync(string email, SecureCredential password, Func<string?> twoFactorProvider, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
}
