using CommunityToolkit.Mvvm.ComponentModel;

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
        ConfigViewModel config)
    {
        Status = status;
        Locations = locations;
        Domains = domains;
        License = license;
        About = about;
        Processes = processes;
        Config = config;
    }
}
