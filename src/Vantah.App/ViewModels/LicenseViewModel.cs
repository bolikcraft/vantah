using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.Core.Auth;
using Vantah.Core.Models;
using Vantah.Core.Vpn;

namespace Vantah.App.ViewModels;

public partial class LicenseViewModel : ErrorAwareViewModel
{
    private const string Empty = "—";

    private readonly IVpnService _vpn;
    private readonly IAuthService _auth;
    private readonly VpnCoordinator _coordinator;

    // Что именно показано в Status/Error, в виде ключей ресурсов: после смены языка
    // те же сообщения пересобираются, не дёргая CLI заново.
    private string? _statusKey = LocKeys.License_Loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLogout))]
    private string _email = Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlan))]
    private string _plan = Empty;

    /// <summary>Плашку тарифа показываем только когда он реально известен, а не заглушка «—».</summary>
    public bool HasPlan => Plan is not (Empty or "");
    [ObservableProperty] private string _devices = Empty;
    [ObservableProperty] private string _renewal = Empty;
    [ObservableProperty] private string _status = Localizer.Instance[LocKeys.License_Loading];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLogout))]
    private bool _isBusy;

    /// <summary>
    /// Выход необратим, поэтому кнопку даём только когда аккаунт реально загружен: пока идёт
    /// запрос и при пустой карточке (ошибка CLI или неразобранный вывод) выходить не из чего.
    /// </summary>
    public bool CanLogout => !IsBusy && Email is not (Empty or "");

    /// <summary>Текущая (пере)загрузка лицензии — чтобы её можно было дождаться в тестах.</summary>
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    public LicenseViewModel(IVpnService vpn, IAuthService auth, VpnCoordinator coordinator)
    {
        _vpn = vpn;
        _auth = auth;
        _coordinator = coordinator;
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(RefreshTexts);
        LoadTask = RefreshAsync(CancellationToken.None);
    }

    // Выход из аккаунта живёт на экране аккаунта (а не в меню «☰»): необратимая команда,
    // подтверждение спрашивает вью перед вызовом. После разлогина зонд координатора сверит
    // состояние и гейт главного окна покажет форму входа.
    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _auth.LogoutAsync();
            await _coordinator.RefreshLoginStateAsync();
        }
        catch
        {
            // Разлогин упал — состояние не меняем; следующий опрос всё равно сверит логин.
        }
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        IsBusy = true;
        SetTexts(status: LocKeys.License_Loading, error: null);
        try
        {
            var license = await _vpn.GetLicenseAsync(ct);
            // Продолжение после await может оказаться на потоке пула (реальный CliRunner ждёт
            // внешний процесс). Email/Plan/Devices/Renewal/Status/Error привязаны к UI, поэтому
            // их правку выполняем строго на UI-потоке — иначе Avalonia роняет cross-thread
            // исключение и вкладка молча остаётся с дефолтами (headless-тесты этого не ловят:
            // там продолжение всегда на UI-потоке).
            await UiThread.RunAsync(() =>
            {
                if (IsBlank(license))
                {
                    // CLI вернул нулевой код, но вывод не распарсился — LicenseParser молча отдаёт
                    // заглушку License("", "UNKNOWN", 0, null); показывать её пользователю нельзя.
                    Clear();
                    SetTexts(status: null, error: LocKeys.License_ErrorNotLoggedIn);
                    return;
                }

                Email = Or(license.Email);
                Plan = Or(license.Plan);
                Devices = license.MaxDevices > 0
                    ? license.MaxDevices.ToString(CultureInfo.InvariantCulture)
                    : Empty;
                Renewal = Or(license.RenewalDate);
                SetTexts(status: null, error: null);
            });
        }
        catch (Exception ex)
        {
            await UiThread.RunAsync(() =>
            {
                Clear();
                SetTexts(status: null, error: LocKeys.License_ErrorFormat, errorArgument: ex.Message);
            });
        }
        finally
        {
            await UiThread.RunAsync(() => IsBusy = false);
        }
    }

    private void Clear()
    {
        Email = Plan = Devices = Renewal = Empty;
        SetTexts(status: null, error: null);
    }

    private void SetTexts(string? status, string? error, string? errorArgument = null)
    {
        _statusKey = status;
        if (error is null) ClearError();
        else if (errorArgument is null) SetError(error);
        else SetError(error, errorArgument);
        RefreshTexts();
    }

    /// <summary>Пересобирает Status из запомненного ключа по текущему языку (Error — база).</summary>
    private void RefreshTexts() => Status = _statusKey is null ? "" : Localizer.Instance[_statusKey];

    private static bool IsBlank(License license) =>
        string.IsNullOrWhiteSpace(license.Email) || license.Plan is "UNKNOWN";

    private static string Or(string? value) => string.IsNullOrWhiteSpace(value) ? Empty : value;
}
