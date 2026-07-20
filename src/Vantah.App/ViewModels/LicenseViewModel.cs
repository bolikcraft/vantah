using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.Core.Models;
using Vantah.Core.Vpn;

namespace Vantah.App.ViewModels;

public partial class LicenseViewModel : ObservableObject
{
    private const string Empty = "—";

    private readonly IVpnService _vpn;

    // Что именно показано в Status/Error, в виде ключей ресурсов: после смены языка
    // те же сообщения пересобираются, не дёргая CLI заново.
    private string? _statusKey = LocKeys.License_Loading;
    private string? _errorKey;
    private string? _errorArgument;

    [ObservableProperty] private string _email = Empty;
    [ObservableProperty] private string _plan = Empty;
    [ObservableProperty] private string _devices = Empty;
    [ObservableProperty] private string _renewal = Empty;
    [ObservableProperty] private string _status = Localizer.Instance[LocKeys.License_Loading];
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;

    /// <summary>Текущая (пере)загрузка лицензии — чтобы её можно было дождаться в тестах.</summary>
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    public LicenseViewModel(IVpnService vpn)
    {
        _vpn = vpn;
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(RefreshTexts);
        LoadTask = RefreshAsync(CancellationToken.None);
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
        _errorKey = error;
        _errorArgument = errorArgument;
        RefreshTexts();
    }

    /// <summary>Пересобирает Status/Error из запомненных ключей по текущему языку.</summary>
    private void RefreshTexts()
    {
        var loc = Localizer.Instance;
        Status = _statusKey is null ? "" : loc[_statusKey];
        Error = _errorKey is null
            ? null
            : _errorArgument is null ? loc[_errorKey] : loc.Format(_errorKey, _errorArgument);
    }

    private static bool IsBlank(License license) =>
        string.IsNullOrWhiteSpace(license.Email) || license.Plan is "UNKNOWN";

    private static string Or(string? value) => string.IsNullOrWhiteSpace(value) ? Empty : value;
}
