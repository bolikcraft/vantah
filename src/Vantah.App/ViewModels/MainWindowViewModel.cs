using CommunityToolkit.Mvvm.ComponentModel;

namespace Vantah.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public StatusViewModel Status { get; }
    public LocationsViewModel Locations { get; }
    public DomainsViewModel Domains { get; }
    public LicenseViewModel License { get; }
    public AboutViewModel About { get; }

    // Индекс активной вкладки (Статус=0, Локации=1, Домены=2, Лицензия=3, О программе=4) —
    // двусторонняя привязка к TabControl; на индексы завязано меню трея.
    [ObservableProperty] private int _selectedTab;

    public MainWindowViewModel(
        StatusViewModel status,
        LocationsViewModel locations,
        DomainsViewModel domains,
        LicenseViewModel license,
        AboutViewModel about)
    {
        Status = status;
        Locations = locations;
        Domains = domains;
        License = license;
        About = about;
    }
}
