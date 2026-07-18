using Vantah.Core.Cli;
using Vantah.Core.Models;
using Vantah.Core.Vpn;    // VpnCommandException

namespace Vantah.Core.Auth;

public sealed class AuthService(ICliRunner cli, IInteractiveCliRunner interactive) : IAuthService
{
    private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(15);
    private readonly LoginSequencer _sequencer = new(interactive);

    public async Task<LoginState> GetLoginStateAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["license"], QuickTimeout, ct);
        return LoginStateParser.Parse(string.IsNullOrWhiteSpace(r.Stdout) ? r.Stderr : r.Stdout);
    }

    public Task<LoginResult> LoginAsync(string email, SecureCredential password, Func<string?> twoFactorProvider, CancellationToken ct = default) =>
        _sequencer.LoginAsync(email, password, twoFactorProvider, ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["logout"], QuickTimeout, ct);
        if (!r.Ok)
            throw new VpnCommandException(
                new[] { r.Stderr, r.Stdout, "logout завершился с ошибкой" }
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))!.Trim());
    }
}
