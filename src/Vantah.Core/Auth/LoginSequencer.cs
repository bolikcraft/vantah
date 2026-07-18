using Vantah.Core.Cli;

namespace Vantah.Core.Auth;

/// <summary>
/// Драйвит интерактивный `login`: логин(email) → пароль → [2FA] → результат.
/// Приглашения/маркеры сверены со строками бинаря adguardvpn-cli v1.7.13 (спайк Task 0):
/// приглашения «Enter username: » / «Enter password: »; 2FA — authenticator app либо код на email.
/// Вся хрупкая логика последовательности — здесь, под тестами на фейковой сессии.
/// </summary>
public sealed class LoginSequencer(IInteractiveCliRunner runner)
{
    private static readonly TimeSpan Overall = TimeSpan.FromMinutes(2);

    private static bool Has(string s, string cue) => s.Contains(cue, StringComparison.OrdinalIgnoreCase);

    public async Task<LoginResult> LoginAsync(
        string email, SecureCredential password, Func<string?> twoFactorProvider, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Overall);
        var token = cts.Token;

        await using var session = await runner.StartAsync(["login"], token);
        try
        {
            var sentEmail = false;
            var sentPassword = false;
            string? chunk;
            while ((chunk = await session.ReadAsync(token)) is not null)
            {
                // Итоговые состояния (проверяем ПЕРВЫМИ — их строки могут содержать слова
                // «email»/«password», по которым иначе сработали бы приглашения).
                if (Has(chunk, "invalid email") || Has(chunk, "email is not val"))
                    return LoginResult.Fail("Неверный адрес email");
                if (Has(chunk, "incorrect password"))
                    return LoginResult.Fail("Неверный пароль");
                if (Has(chunk, "credentials are invalid") || Has(chunk, "user not found"))
                    return LoginResult.Fail("Неверные учётные данные");
                if (Has(chunk, "this code is invalid") || Has(chunk, "incorrect 2fa"))
                    return LoginResult.Fail("Неверный код подтверждения");
                if (Has(chunk, "account must be confirmed"))
                    return LoginResult.Fail("Аккаунт не подтверждён — перейдите по ссылке из письма");
                if (Has(chunk, "account is temporarily blocked") || Has(chunk, "account is temporarily locked") || Has(chunk, "too many attempts"))
                    return LoginResult.Fail("Аккаунт временно заблокирован, попробуйте позже");
                if (Has(chunk, "failed to log in"))
                    return LoginResult.Fail("Не удалось войти");
                if (Has(chunk, "logged in"))
                    return LoginResult.Ok();

                // Приглашения ввода.
                if (!sentEmail && (Has(chunk, "username") || Has(chunk, "email")))
                {
                    await session.WriteLineAsync(email, token);
                    sentEmail = true;
                }
                else if (!sentPassword && Has(chunk, "password"))
                {
                    var chars = password.Reveal();
                    await session.WriteLineAsync(chars, token);
                    password.Clear();                     // занулить сразу после отправки
                    sentPassword = true;
                }
                else if (Has(chunk, "2fa") || Has(chunk, "verification code") ||
                         Has(chunk, "authenticator") || Has(chunk, "enter the code"))
                {
                    var code = twoFactorProvider() ?? "";
                    await session.WriteLineAsync(code, token);
                }
            }

            var exit = await session.WaitForExitAsync(token);
            return exit == 0 ? LoginResult.Ok() : LoginResult.Fail("Вход не выполнен");
        }
        finally
        {
            password.Clear();                             // гарантия зануления на любом пути
        }
    }
}
