using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Services;
using Vantah.Core.Auth;

namespace Vantah.App.ViewModels;

/// <summary>
/// Экран входа через браузер (device-code). Нажатие «Войти» запускает `login`, получает ссылку
/// авторизации, открывает её в браузере и ждёт, пока пользователь подтвердит вход. Секретов нет —
/// пароль в этом флоу не вводится.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly VpnCoordinator _coordinator;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private bool _isAwaitingAuth;   // ссылка получена, ждём подтверждения
    [ObservableProperty] private string? _url;
    [ObservableProperty] private string? _userCode;
    [ObservableProperty] private string? _error;

    // Открытие ссылки в браузере — через окно (Launcher); поставляет App.axaml.cs.
    public Func<string, Task>? BrowserOpener { get; set; }

    public LoginViewModel(IAuthService auth, VpnCoordinator coordinator)
    {
        _auth = auth;
        _coordinator = coordinator;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsAwaitingAuth) return;                       // уже идёт вход
        Error = null;
        _cts = new CancellationTokenSource();
        try
        {
            var result = await _auth.LoginAsync(OnPrompt, _cts.Token);
            if (result.Success)
                await _coordinator.RefreshLoginStateAsync();   // гейт спрячет форму
            else
                Error = result.Message;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsAwaitingAuth = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // Колбэк из ядра обычно приходит с фонового потока (чтение вывода процесса) — тогда кладём на
    // UI-поток через Dispatcher. Если уже на UI-потоке — применяем сразу, чтобы не разъехаться по
    // порядку с завершением входа (иначе отложенный Post мог бы сработать после сброса состояния).
    private void OnPrompt(DeviceCodePrompt prompt)
    {
        if (Dispatcher.UIThread.CheckAccess()) ApplyPrompt(prompt);
        else Dispatcher.UIThread.Post(() => ApplyPrompt(prompt));
    }

    private void ApplyPrompt(DeviceCodePrompt prompt)
    {
        Url = prompt.Url;
        UserCode = prompt.UserCode;
        IsAwaitingAuth = true;
        _ = OpenBrowserAsync();                           // открыть один раз автоматически
    }

    [RelayCommand]
    private async Task OpenBrowserAsync()
    {
        if (Url is { } u && BrowserOpener is { } open)
        {
            try { await open(u); } catch { /* пользователь откроет ссылку вручную */ }
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();
}
