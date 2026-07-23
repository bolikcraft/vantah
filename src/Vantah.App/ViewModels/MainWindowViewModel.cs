using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
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

        store.Changed += (_, s) => Dispatcher.UIThread.Post(
            () => IsLoggedIn = s.LoginState != LoginState.LoggedOut);
        IsLoggedIn = store.Current.LoginState != LoginState.LoggedOut;
    }

    // Домены = вкладка 2 (см. комментарий у SelectedTab). Если список исключений при старте
    // не пришёл (таймаут CLI), возврат на вкладку — естественный момент повторить попытку.
    partial void OnSelectedTabChanged(int value)
    {
        if (value == 2) Domains.ReloadIfFailed();
    }
}
