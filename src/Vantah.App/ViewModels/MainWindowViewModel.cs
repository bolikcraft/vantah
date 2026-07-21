using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Services;
using Vantah.Core.Auth;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly VpnCoordinator _coordinator;

    public StatusViewModel Status { get; }
    public LocationsViewModel Locations { get; }
    public DomainsViewModel Domains { get; }
    public LicenseViewModel License { get; }
    public AboutViewModel About { get; }
    public ProcessesViewModel Processes { get; }
    public ConfigViewModel Config { get; }
    public LoginViewModel Login { get; }

    /// <summary>Плашка «доступна новая версия Vantah»; null — проверка обновлений не подключена.</summary>
    public UpdateBannerViewModel? UpdateBanner { get; }

    // Гейт формы входа. Гейтимся по «!= LoggedOut» (а не «== LoggedIn»), чтобы при Unknown
    // (CLI недоступен/зонд не прошёл) не мигать формой входа зря.
    [ObservableProperty] private bool _isLoggedIn = true;

    // Индекс активной вкладки (Статус=0, Локации=1, Домены=2) — двусторонняя привязка к TabControl;
    // на индексы завязано меню трея, поэтому новые вкладки добавляем в конец. Служебные экраны
    // вкладками больше не являются: они живут в меню «☰» и открываются отдельными окнами.
    [ObservableProperty] private int _selectedTab;

    public MainWindowViewModel(
        StatusViewModel status,
        LocationsViewModel locations,
        DomainsViewModel domains,
        LicenseViewModel license,
        AboutViewModel about,
        ProcessesViewModel processes,
        ConfigViewModel config,
        LoginViewModel login,
        IAuthService auth,
        VpnCoordinator coordinator,
        AppStateStore store,
        UpdateBannerViewModel? updateBanner = null)
    {
        Status = status;
        Locations = locations;
        Domains = domains;
        License = license;
        About = about;
        Processes = processes;
        Config = config;
        Login = login;
        UpdateBanner = updateBanner;
        _auth = auth;
        _coordinator = coordinator;

        store.Changed += (_, s) => Dispatcher.UIThread.Post(
            () => IsLoggedIn = s.LoginState != LoginState.LoggedOut);
        IsLoggedIn = store.Current.LoginState != LoginState.LoggedOut;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _auth.LogoutAsync();
            await _coordinator.RefreshLoginStateAsync();   // гейт покажет форму входа
        }
        catch
        {
            // Разлогин упал — состояние не меняем; следующий опрос всё равно сверит логин.
        }
    }
}
