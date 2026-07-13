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
using Vantah.Core.Locations;
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

    // Дефолты повторяют прежнее жёсткое поведение: избранные сверху, дальше по возрастанию пинга.
    [ObservableProperty] private LocationSortKey _sortKey = LocationSortKey.Ping;
    [ObservableProperty] private bool _sortAscending = true;
    [ObservableProperty] private bool _favoritesFirst = true;

    public ObservableCollection<LocationItemViewModel> Items { get; } = new();

    public string IsoHeader => Header("ISO", LocationSortKey.Iso);
    public string CityHeader => Header("Город", LocationSortKey.City);
    public string CountryHeader => Header("Страна", LocationSortKey.Country);
    public string PingHeader => Header("Пинг (мс)", LocationSortKey.Ping);
    public string FavoritesHeader => FavoritesFirst ? "★ ▲" : "★";

    private string Header(string text, LocationSortKey key) =>
        SortKey == key ? text + (SortAscending ? " ▲" : " ▼") : text;

    public LocationsViewModel(IVpnService vpn, VpnCoordinator coordinator, FavoritesStore favorites, AppStateStore store)
    {
        _vpn = vpn; _coordinator = coordinator; _favorites = favorites; _store = store;
        _store.Changed += (_, s) => Dispatcher.UIThread.Post(() => ApplyConnected(s));
        _ = LoadAsync();
    }

    partial void OnSearchChanged(string value) => ApplyFilter();

    partial void OnSortKeyChanged(LocationSortKey value)
    {
        RaiseHeadersChanged();
        ApplyFilter();
    }

    partial void OnSortAscendingChanged(bool value)
    {
        RaiseHeadersChanged();
        ApplyFilter();
    }

    partial void OnFavoritesFirstChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoritesHeader));
        ApplyFilter();
    }

    private void RaiseHeadersChanged()
    {
        OnPropertyChanged(nameof(IsoHeader));
        OnPropertyChanged(nameof(CityHeader));
        OnPropertyChanged(nameof(CountryHeader));
        OnPropertyChanged(nameof(PingHeader));
    }

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
        var filtered = q.ToList();
        var favoriteKeys = filtered.Where(i => i.IsFavorite).Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        var sorted = LocationSorter.Sort(
            filtered.Select(i => i.Model).ToList(), SortKey, SortAscending, FavoritesFirst, favoriteKeys);

        // Ключ локации теоретически может повториться — раскладываем по очереди на ключ,
        // чтобы каждой отсортированной доменной модели достался свой элемент списка.
        var byKey = new Dictionary<string, Queue<LocationItemViewModel>>(StringComparer.Ordinal);
        foreach (var i in filtered)
        {
            if (!byKey.TryGetValue(i.Key, out var queue))
                byKey[i.Key] = queue = new Queue<LocationItemViewModel>();
            queue.Enqueue(i);
        }

        Items.Clear();
        foreach (var loc in sorted)
            if (byKey.TryGetValue(loc.Key, out var queue) && queue.Count > 0)
                Items.Add(queue.Dequeue());
    }

    [RelayCommand]
    private void SortBy(string column)
    {
        var key = column switch
        {
            "Iso" => LocationSortKey.Iso,
            "Country" => LocationSortKey.Country,
            "City" => LocationSortKey.City,
            "Ping" => LocationSortKey.Ping,
            _ => (LocationSortKey?)null
        };
        if (key is null)
            return;

        if (SortKey == key.Value)
            SortAscending = !SortAscending;
        else
        {
            SortAscending = true;
            SortKey = key.Value;
        }
    }

    [RelayCommand]
    private void ToggleFavoritesFirst() => FavoritesFirst = !FavoritesFirst;

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
