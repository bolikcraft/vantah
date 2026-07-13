using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.Core.Models;
using Vantah.Core.Vpn;

namespace Vantah.App.ViewModels;

public partial class LicenseViewModel : ObservableObject
{
    private const string Empty = "—";

    private readonly IVpnService _vpn;

    [ObservableProperty] private string _email = Empty;
    [ObservableProperty] private string _plan = Empty;
    [ObservableProperty] private string _devices = Empty;
    [ObservableProperty] private string _renewal = Empty;
    [ObservableProperty] private string _status = "Загрузка лицензии…";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;

    public LicenseViewModel(IVpnService vpn)
    {
        _vpn = vpn;
        _ = RefreshAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        Status = "Загрузка лицензии…";
        try
        {
            var license = await _vpn.GetLicenseAsync(ct);
            if (IsBlank(license))
            {
                // CLI вернул нулевой код, но вывод не распарсился — LicenseParser молча отдаёт
                // заглушку License("", "UNKNOWN", 0, null); показывать её пользователю нельзя.
                Clear();
                Error = "Не удалось получить лицензию — возможно, не выполнен вход";
                return;
            }

            Email = Or(license.Email);
            Plan = Or(license.Plan);
            Devices = license.MaxDevices > 0
                ? license.MaxDevices.ToString(CultureInfo.InvariantCulture)
                : Empty;
            Renewal = Or(license.RenewalDate);
            Status = "";
        }
        catch (Exception ex)
        {
            Clear();
            Error = "Не удалось получить лицензию: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Clear()
    {
        Email = Plan = Devices = Renewal = Empty;
        Status = "";
    }

    private static bool IsBlank(License license) =>
        string.IsNullOrWhiteSpace(license.Email) || license.Plan is "UNKNOWN";

    private static string Or(string? value) => string.IsNullOrWhiteSpace(value) ? Empty : value;
}
