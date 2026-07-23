using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
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

    // Взводится, когда SortBy меняет колонку и направление разом: пересортировка — одна, в конце.
    private bool _suspendApplyFilter;

    [ObservableProperty] private string _search = "";

    // Состояние загрузки локаций для самого экрана «Локации»: пока грузим — показываем «Загрузка…»,
    // при сбое — текст ошибки и кнопку «Обновить», а не молчаливо пустой список.
    [ObservableProperty] private bool _isLoading;
    // Причина сбоя, а не готовая строка: после смены языка сообщение пересобирается (см. RaiseHeadersChanged).
    private UiText _loadErrorValue;
    [ObservableProperty] private string? _loadError;

    /// <summary>Текущая (пере)загрузка списка — чтобы её можно было дождаться в тестах.</summary>
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    // Дефолты повторяют прежнее жёсткое поведение: избранные сверху, дальше по возрастанию пинга.
    [ObservableProperty] private LocationSortKey _sortKey = LocationSortKey.Ping;
    [ObservableProperty] private bool _sortAscending = true;
    [ObservableProperty] private bool _favoritesFirst = true;

    public ObservableCollection<LocationItemViewModel> Items { get; } = new();

    // Пока статус подключения не определён (Unknown — до первого ответа CLI), кнопки
    // «Подключить/Отключить» в строках заблокированы: иначе список уже предлагает подключиться,
    // хотя туннель, возможно, уже поднят. Разблокируются, как только статус становится известен.
    [ObservableProperty] private bool _isStatusKnown;

    /// <summary>Строки-заглушки «скелетона» на время загрузки (значения не важны, важно их число).</summary>
    public IReadOnlyList<int> SkeletonRows { get; } = Enumerable.Range(0, 9).ToList();

    public string IsoHeader => Header(LocKeys.Locations_Col_Iso, LocationSortKey.Iso);
    public string CityHeader => Header(LocKeys.Locations_Col_City, LocationSortKey.City);
    public string CountryHeader => Header(LocKeys.Locations_Col_Country, LocationSortKey.Country);
    public string PingHeader => Header(LocKeys.Locations_Col_Ping, LocationSortKey.Ping);
    public string FavoritesHeader => FavoritesFirst ? "★ ▲" : "★";

    // Стрелка направления приклеивается к локализованному названию колонки.
    private string Header(string key, LocationSortKey sortKey)
    {
        var text = Localizer.Instance[key];
        return SortKey == sortKey ? text + (SortAscending ? " ▲" : " ▼") : text;
    }

    public LocationsViewModel(IVpnService vpn, VpnCoordinator coordinator, FavoritesStore favorites, AppStateStore store)
    {
        _vpn = vpn; _coordinator = coordinator; _favorites = favorites; _store = store;
        _store.Changed += (_, s) => Dispatcher.UIThread.Post(() => ApplyConnected(s));
        // Заголовки колонок собираются в коде (название + стрелка сортировки) — после
        // смены языка просим их перечитать себя.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(RaiseHeadersChanged);
        LoadTask = LoadAsync();
    }

    partial void OnSearchChanged(string value) => ApplyFilter();

    partial void OnSortKeyChanged(LocationSortKey value)
    {
        RaiseHeadersChanged();
        if (!_suspendApplyFilter) ApplyFilter();
    }

    partial void OnSortAscendingChanged(bool value)
    {
        RaiseHeadersChanged();
        if (!_suspendApplyFilter) ApplyFilter();
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
        LoadError = _loadErrorValue.Text;   // сообщение о сбое — тоже на новом языке
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        _loadErrorValue = UiText.None;
        LoadError = null;
        try
        {
            var favs = _favorites.Load();
            var locs = await _vpn.GetLocationsAsync();
            // Продолжение после await может оказаться на потоке пула (реальный CliRunner ждёт
            // внешний процесс). Список Items привязан к UI, поэтому его правку выполняем строго
            // на UI-потоке — иначе Avalonia роняет cross-thread исключение и список молча
            // остаётся пустым (headless-тесты этого не ловят: там продолжение всегда на UI-потоке).
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _all.Clear();
                foreach (var l in locs)
                    _all.Add(new LocationItemViewModel(l) { IsFavorite = favs.Contains(l.Key) });
                _coordinator.UpdateKnownLocations(locs);
                ApplyFilter();
                ApplyConnected(_store.Current);
            });
        }
        catch (Exception ex)
        {
            // Ошибку показываем и на самом экране «Локации» (LoadError + кнопка «Обновить»),
            // и в общем баннере на «Статусе».
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var error = UiText.From(ex);
                _loadErrorValue = error;
                LoadError = error.Text;
                _store.Set(s => s with { Error = Vantah.Core.Errors.AppError.From(ex) });
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    /// <summary>Повторная загрузка локаций (кнопка «Обновить» на экране и после сбоя).</summary>
    [RelayCommand]
    private Task Reload() => LoadTask = LoadAsync();

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

        // Key = "ISO|City" уникален, поэтому отсортированные доменные модели
        // раскладываем обратно в элементы списка простым поиском по ключу.
        var byKey = new Dictionary<string, LocationItemViewModel>(StringComparer.Ordinal);
        foreach (var i in filtered)
            byKey[i.Key] = i;

        Items.Clear();
        foreach (var loc in sorted)
            if (byKey.TryGetValue(loc.Key, out var item))
                Items.Add(item);
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

        // Смена колонки трогает сразу два свойства. Подавляем промежуточный ApplyFilter():
        // каждая пересборка Items роняет Reset в ListBox и сбрасывает скролл и выделение.
        _suspendApplyFilter = true;
        try
        {
            SortAscending = SortKey == key.Value ? !SortAscending : true;
            SortKey = key.Value;
        }
        finally
        {
            _suspendApplyFilter = false;
        }

        ApplyFilter();
    }

    [RelayCommand]
    private void ToggleFavoritesFirst() => FavoritesFirst = !FavoritesFirst;

    private void ApplyConnected(AppSnapshot s)
    {
        IsStatusKnown = s.Connection != ConnectionState.Unknown;
        var connectedCity = s.Connection == ConnectionState.Connected ? s.Location : null;
        foreach (var item in _all)
            item.IsConnected = connectedCity is not null &&
                string.Equals(item.City, connectedCity, StringComparison.OrdinalIgnoreCase);
    }

    // Кнопка строки одна на оба действия: для активной локации — «Отключить», для остальных —
    // «Подключить». Так подпись всегда совпадает с реальным состоянием строки.
    [RelayCommand]
    private Task ToggleConnection(LocationItemViewModel item) =>
        item.IsConnected
            ? _coordinator.DisconnectAsync()
            : _coordinator.ConnectAsync(item.City, fastest: false);

    [RelayCommand]
    private void ToggleFavorite(LocationItemViewModel item)
    {
        item.IsFavorite = !item.IsFavorite;
        _favorites.Save(_all.Where(i => i.IsFavorite).Select(i => i.Key));
        ApplyFilter();
    }
}
