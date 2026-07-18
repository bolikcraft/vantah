using System.Text;
using Vantah.Core.Cli;
using Vantah.Core.Models;
using Vantah.Core.Vpn;    // VpnCommandException

namespace Vantah.Core.Auth;

public sealed class AuthService(ICliRunner cli, IInteractiveCliRunner interactive) : IAuthService
{
    private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(15);
    // Верхняя граница ожидания входа: ссылка device-code живёт ~28 мин, берём с запасом.
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(30);

    public async Task<LoginState> GetLoginStateAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["license"], QuickTimeout, ct);
        return LoginStateParser.Parse(string.IsNullOrWhiteSpace(r.Stdout) ? r.Stderr : r.Stdout);
    }

    public async Task<LoginResult> LoginAsync(Action<DeviceCodePrompt> onPrompt, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(LoginTimeout);
        var token = cts.Token;

        await using var session = await interactive.StartAsync(["login"], token);
        var buffer = new StringBuilder();
        var reported = false;
        try
        {
            string? chunk;
            while ((chunk = await session.ReadAsync(token)) is not null)
            {
                if (reported) continue;                     // ссылку уже отдали — просто дочитываем до выхода
                buffer.Append(chunk);
                if (DeviceCodeParser.Parse(buffer.ToString()) is { } prompt)
                {
                    reported = true;
                    onPrompt(prompt);
                }
            }

            // Потоки закрылись — процесс завершается. Успех: код 0 либо зонд подтверждает вход
            // (на точную строку успеха не завязываемся — она может меняться между версиями CLI).
            var exit = await session.WaitForExitAsync(token);
            if (exit == 0) return LoginResult.Ok();
            var state = await GetLoginStateAsync(ct);
            return state == LoginState.LoggedIn ? LoginResult.Ok() : LoginResult.Fail("Вход не выполнен");
        }
        catch (OperationCanceledException)
        {
            // Грациозно просим CLI отменить (`x`), затем DisposeAsync добьёт процесс.
            try { await session.WriteLineAsync("x", CancellationToken.None); } catch { /* процесс мог уже уйти */ }
            return ct.IsCancellationRequested
                ? LoginResult.Fail("Вход отменён")
                : LoginResult.Fail("Истекло время ожидания входа");
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["logout"], QuickTimeout, ct);
        if (!r.Ok)
            throw new VpnCommandException(
                new[] { r.Stderr, r.Stdout, "logout завершился с ошибкой" }
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))!.Trim());
    }
}
