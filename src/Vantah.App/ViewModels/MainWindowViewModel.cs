using CommunityToolkit.Mvvm.ComponentModel;

namespace Vantah.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public StatusViewModel Status { get; }

    public MainWindowViewModel(StatusViewModel status)
    {
        Status = status;
    }
}
