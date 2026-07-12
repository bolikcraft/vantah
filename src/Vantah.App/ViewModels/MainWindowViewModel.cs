using CommunityToolkit.Mvvm.ComponentModel;

namespace Vantah.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public StatusViewModel Status { get; }
    public LocationsViewModel Locations { get; }
    public DomainsViewModel Domains { get; }

    public MainWindowViewModel(StatusViewModel status, LocationsViewModel locations, DomainsViewModel domains)
    {
        Status = status;
        Locations = locations;
        Domains = domains;
    }
}
