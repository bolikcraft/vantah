using CommunityToolkit.Mvvm.ComponentModel;

namespace Vantah.App.ViewModels;

public partial class DomainItemViewModel(string domain) : ObservableObject
{
    public string Domain { get; } = domain;
}
