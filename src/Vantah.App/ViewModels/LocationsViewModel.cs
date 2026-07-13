using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Services;
using Vantah.Core.Favorites;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Vpn;

namespace Vantah.App.ViewModels;

public partial class LocationsViewModel : ObservableObject
{
    private readonly IVpnService _vpn;
    private readonly VpnCoordinator _coordinator;
    private readonly FavoritesStore _favorites;
    private readonly AppStateStore _store;
    private readonly List<LocationItemViewModel> _all = new();

    [ObservableProperty] private string _search = "";
    public ObservableCollection<LocationItemViewModel> Items { get; } = new();

    public LocationsViewModel(IVpnService vpn, VpnCoordinator coordinator, FavoritesStore favorites, AppStateStore store)
    {
        _vpn = vpn; _coordinator = coordinator; _favorites = favorites; _store = store;
        _store.Changed += (_, s) => Dispatcher.UIThread.Post(() => ApplyConnected(s));
        _ = LoadAsync();
    }

    partial void OnSearchChanged(string value) => ApplyFilter();

    private async Task LoadAsync()
    {
        try
        {
            var favs = _favorites.Load();
            var locs = await _vpn.GetLocationsAsync();
            _all.Clear();
            foreach (var l in locs)
                _all.Add(new LocationItemViewModel(l) { IsFavorite = favs.Contains(l.Key) });
            _coordinator.UpdateKnownLocations(locs);
            ApplyFilter();
            ApplyConnected(_store.Current);
        }
        catch (Exception ex)
        {
            // Ошибка загрузки локаций (нет CLI / не залогинен / таймаут) —
            // показываем в баннере на вкладке «Статус».
            _store.Set(s => s with { Error = ex.Message });
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<LocationItemViewModel> q = _all;
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            q = q.Where(i =>
                i.City.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                i.Country.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                i.IsoCode.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        q = q.OrderByDescending(i => i.IsFavorite).ThenBy(i => i.PingMs);
        Items.Clear();
        foreach (var i in q) Items.Add(i);
    }

    private void ApplyConnected(AppSnapshot s)
    {
        var connectedCity = s.Connection == ConnectionState.Connected ? s.Location : null;
        foreach (var item in _all)
            item.IsConnected = connectedCity is not null &&
                string.Equals(item.City, connectedCity, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private Task Connect(LocationItemViewModel item) =>
        _coordinator.ConnectAsync(item.City, fastest: false);

    [RelayCommand]
    private void ToggleFavorite(LocationItemViewModel item)
    {
        item.IsFavorite = !item.IsFavorite;
        _favorites.Save(_all.Where(i => i.IsFavorite).Select(i => i.Key));
        ApplyFilter();
    }
}
