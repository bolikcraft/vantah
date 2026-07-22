using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.Core.Auth;

namespace Vantah.App.ViewModels;

/// <summary>
/// Экран входа через браузер (device-code). Нажатие «Войти» запускает `login`, получает ссылку
/// авторизации, открывает её в браузере и ждёт, пока пользователь подтвердит вход. Секретов нет —
/// пароль в этом флоу не вводится.
/// </summary>
public partial class LoginViewModel : ErrorAwareViewModel
{
    private readonly IAuthService _auth;
    private readonly VpnCoordinator _coordinator;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private bool _isAwaitingAuth;   // ссылка получена, ждём подтверждения
    [ObservableProperty] private string? _url;
    [ObservableProperty] private string? _userCode;

    // Открытие ссылки в браузере — через окно (Launcher); поставляет App.axaml.cs.
    public Func<string, Task>? BrowserOpener { get; set; }

    // Копирование в буфер обмена — тоже через окно (TopLevel.Clipboard); поставляет App.axaml.cs.
    // Делегат, а не TopLevel во вьюмодели: так копирование проверяется тестом с фейком.
    public Func<string, Task>? ClipboardWriter { get; set; }

    public LoginViewModel(IAuthService auth, VpnCoordinator coordinator)
    {
        _auth = auth;
        _coordinator = coordinator;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsAwaitingAuth) return;                       // уже идёт вход
        ClearError();
        _cts = new CancellationTokenSource();
        try
        {
            var result = await _auth.LoginAsync(OnPrompt, _cts.Token);
            if (result.Success)
                await _coordinator.RefreshLoginStateAsync();   // гейт спрячет форму
            else
                SetError(UiText.Of(result.Message));
        }
        catch (Exception ex)
        {
            SetError(ex);
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

    // Запасной путь, когда браузер не открылся: ссылка видна на экране, её можно скопировать.
    [RelayCommand]
    private async Task CopyUrlAsync()
    {
        if (Url is { Length: > 0 } u && ClipboardWriter is { } copy)
        {
            try { await copy(u); } catch { /* ссылку всё ещё можно выделить и скопировать вручную */ }
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();
}
