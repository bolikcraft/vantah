using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [ObservableProperty] private bool _isError;   // красит строку статуса в красный
    [ObservableProperty] private bool _isBusy;

    public LicenseViewModel(IVpnService vpn)
    {
        _vpn = vpn;
        _ = RefreshAsync();
    }

    // `adguardvpn-cli license` ходит в сеть — грузим асинхронно, результат кладём в UI-поток.
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        IsError = false;
        Status = "Загрузка лицензии…";
        try
        {
            var license = await _vpn.GetLicenseAsync();
            Dispatcher.UIThread.Post(() =>
            {
                Email = Or(license.Email);
                Plan = Or(license.Plan);
                Devices = license.MaxDevices > 0
                    ? license.MaxDevices.ToString(CultureInfo.InvariantCulture)
                    : Empty;
                Renewal = Or(license.RenewalDate);
                Status = "";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsError = true;
                Status = "Не удалось получить лицензию: " + ex.Message;
            });
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsBusy = false);
        }
    }

    private static string Or(string? value) => string.IsNullOrWhiteSpace(value) ? Empty : value;
}
