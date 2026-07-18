using Vantah.Core.Models;

namespace Vantah.Core.Auth;

public interface IAuthService
{
    Task<LoginState> GetLoginStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Запускает вход через браузер (device-code). Когда CLI выдаёт ссылку авторизации —
    /// вызывает <paramref name="onPrompt"/> (один раз). Затем ждёт, пока пользователь подтвердит
    /// вход в браузере и CLI завершится. Отмена через <paramref name="ct"/>.
    /// </summary>
    Task<LoginResult> LoginAsync(Action<DeviceCodePrompt> onPrompt, CancellationToken ct = default);

    Task LogoutAsync(CancellationToken ct = default);
}
