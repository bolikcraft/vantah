using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.Core.Models;

namespace Vantah.App.ViewModels;

public partial class LocationItemViewModel : ObservableObject
{
    public LocationItemViewModel(Location loc)
    {
        Model = loc;
        Flag = LoadFlag(loc.IsoCode);
    }

    public Location Model { get; }
    public string IsoCode => Model.IsoCode;
    public string Country => Model.Country;
    public string City => Model.City;
    public int PingMs => Model.PingMs;
    public string Key => Model.Key;

    public Bitmap? Flag { get; }

    [ObservableProperty] private bool _isFavorite;

    [ObservableProperty] private bool _isConnected;

    public string Star => IsFavorite ? "★" : "☆";

    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(Star));

    private static Bitmap? LoadFlag(string isoCode)
    {
        if (string.IsNullOrWhiteSpace(isoCode))
            return null;
        try
        {
            var uri = new Uri($"avares://Vantah.App/Assets/flags/{isoCode.ToLowerInvariant()}.png");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
