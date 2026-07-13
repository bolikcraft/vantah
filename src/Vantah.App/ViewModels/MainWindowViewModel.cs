using CommunityToolkit.Mvvm.ComponentModel;

namespace Vantah.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public StatusViewModel Status { get; }
    public LocationsViewModel Locations { get; }
    public DomainsViewModel Domains { get; }
    public HistoryViewModel History { get; }

    // Индекс активной вкладки (Статус=0, Локации=1, Домены=2, История=3) — двусторонняя привязка к TabControl.
    [ObservableProperty] private int _selectedTab;

    public MainWindowViewModel(StatusViewModel status, LocationsViewModel locations, DomainsViewModel domains, HistoryViewModel history)
    {
        Status = status;
        Locations = locations;
        Domains = domains;
        History = history;
    }
}
