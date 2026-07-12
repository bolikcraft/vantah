using CommunityToolkit.Mvvm.ComponentModel;

namespace Vantah.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public StatusViewModel Status { get; }
    public LocationsViewModel Locations { get; }

    public MainWindowViewModel(StatusViewModel status, LocationsViewModel locations)
    {
        Status = status;
        Locations = locations;
    }
}
