using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.Core.Models;

namespace Vantah.App.ViewModels;

public partial class LocationItemViewModel(Location loc) : ObservableObject
{
    public Location Model { get; } = loc;
    public string IsoCode => Model.IsoCode;
    public string Country => Model.Country;
    public string City => Model.City;
    public int PingMs => Model.PingMs;
    public string Key => Model.Key;

    [ObservableProperty] private bool _isFavorite;

    public string Star => IsFavorite ? "★" : "☆";

    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(Star));
}
