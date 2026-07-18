using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.Core.Auth;

namespace Vantah.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly VpnCoordinator _coordinator;

    [ObservableProperty] private string _email = "";
    // Код 2FA/подтверждения по email вводится в обычный TextBox: он приходит ПОСЛЕ email+пароля,
    // поэтому поле показываем всегда как необязательное — если CLI его запросит, пользователь
    // заполняет и повторяет вход (TOTP) либо вводит присланный на почту код и входит снова.
    [ObservableProperty] private string _twoFactorCode = "";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;

    // Пароль поставляет View (PasswordBox) как char[] — VM не хранит его в string.
    public Func<char[]>? PasswordProvider { get; set; }
    // View сбрасывает поле пароля после отправки.
    public Action? PasswordCleared { get; set; }

    public LoginViewModel(IAuthService auth, VpnCoordinator coordinator)
    {
        _auth = auth;
        _coordinator = coordinator;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        Error = null;
        if (string.IsNullOrWhiteSpace(Email))
        {
            Error = Localizer.Instance[LocKeys.Login_EmptyEmail];
            return;
        }
        var chars = PasswordProvider?.Invoke() ?? Array.Empty<char>();
        if (chars.Length == 0)
        {
            Error = Localizer.Instance[LocKeys.Login_EmptyPassword];
            return;
        }

        IsBusy = true;
        try
        {
            using var cred = new SecureCredential(chars);   // занулится внутри секвенсора и по Dispose
            var result = await _auth.LoginAsync(
                Email.Trim(), cred,
                twoFactorProvider: () => string.IsNullOrWhiteSpace(TwoFactorCode) ? null : TwoFactorCode.Trim());

            if (result.Success)
            {
                TwoFactorCode = "";
                PasswordCleared?.Invoke();
                await _coordinator.RefreshLoginStateAsync();   // гейт спрячет форму
            }
            else
            {
                Error = result.Message;   // причина из CLI, без эха пароля
            }
        }
        catch (OperationCanceledException)
        {
            // Общий таймаут входа (2 мин) — не показываем англоязычное «A task was canceled».
            Error = Localizer.Instance[LocKeys.Login_Timeout];
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
