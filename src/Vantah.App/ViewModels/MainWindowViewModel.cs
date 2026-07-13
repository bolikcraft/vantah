using CommunityToolkit.Mvvm.ComponentModel;

namespace Vantah.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public StatusViewModel Status { get; }
    public LocationsViewModel Locations { get; }
    public DomainsViewModel Domains { get; }

    // Индекс активной вкладки (Статус=0, Локации=1, Домены=2) — двусторонняя привязка к TabControl.
    [ObservableProperty] private int _selectedTab;

    public MainWindowViewModel(StatusViewModel status, LocationsViewModel locations, DomainsViewModel domains)
    {
        Status = status;
        Locations = locations;
        Domains = domains;
    }
}
