using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.ViewModels;

public partial class DomainsViewModel : ObservableObject
{
    private readonly IExclusionsService _exclusions;
    private readonly ExclusionsStore _store;
    private readonly AppStateStore _appState;
    private readonly List<string> _all = new();

    private SiteExclusionMode _mode = SiteExclusionMode.General;
    private bool _switchingMode;   // защита от реентранта при программной установке радио

    [ObservableProperty] private string _query = "";  // одно поле: ввод домена и фильтр списка
    [ObservableProperty] private bool _isGeneral = true;
    [ObservableProperty] private bool _isSelective;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    public ObservableCollection<DomainItemViewModel> Items { get; } = new();

    public DomainsViewModel(IExclusionsService exclusions, ExclusionsStore store, AppStateStore appState)
    {
        _exclusions = exclusions;
        _store = store;
        _appState = appState;
        _ = ReloadAsync();
    }

    partial void OnQueryChanged(string value) => ApplyFilter();

    partial void OnIsGeneralChanged(bool value)
    {
        if (value && !_switchingMode) _ = SwitchModeAsync(SiteExclusionMode.General);
    }

    partial void OnIsSelectiveChanged(bool value)
    {
        if (value && !_switchingMode) _ = SwitchModeAsync(SiteExclusionMode.Selective);
    }

    private async Task ReloadAsync()
    {
        try
        {
            IsBusy = true; Error = null;
            var snap = await _exclusions.GetAsync();
            _mode = snap.Mode;
            _switchingMode = true;
            IsGeneral = snap.Mode == SiteExclusionMode.General;
            IsSelective = snap.Mode == SiteExclusionMode.Selective;
            _switchingMode = false;

            _all.Clear();
            _all.AddRange(snap.Domains);
            ApplyFilter();
            _appState.Set(s => s with { ExclusionsCount = _all.Count });
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        IEnumerable<string> q = _all;
        if (!string.IsNullOrWhiteSpace(Query))
            q = q.Where(d => d.Contains(Query.Trim(), StringComparison.OrdinalIgnoreCase));
        Items.Clear();
        foreach (var d in q) Items.Add(new DomainItemViewModel(d));
    }

    private async Task SwitchModeAsync(SiteExclusionMode target)
    {
        if (target == _mode) return;
        try
        {
            IsBusy = true; Error = null;
            await _exclusions.SetModeAsync(_mode, target, _all.ToList());
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            // Переключение упало: CLI/_mode остались на прежнем режиме — вернуть радио к нему,
            // иначе визуально «залипнет» на целевом, а повторный клик того же радио ничего не запустит.
            Error = ex.Message;
            _switchingMode = true;
            IsGeneral = _mode == SiteExclusionMode.General;
            IsSelective = _mode == SiteExclusionMode.Selective;
            _switchingMode = false;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Add()
    {
        var domain = Query.Trim();
        if (domain.Length == 0) return;
        try { await _exclusions.AddAsync(domain); Query = ""; await ReloadAsync(); }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task Remove(DomainItemViewModel item)
    {
        try { await _exclusions.RemoveAsync(item.Domain); await ReloadAsync(); }
        catch (Exception ex) { Error = ex.Message; }
    }

    // Полную очистку с подтверждением инициирует View (диалог), сюда приходит уже подтверждённый вызов.
    [RelayCommand]
    private async Task Clear()
    {
        try { foreach (var d in _all.ToList()) await _exclusions.RemoveAsync(d); await ReloadAsync(); }
        catch (Exception ex) { Error = ex.Message; }
    }

    // Текст берётся из буфера обмена во View (TopLevel.Clipboard) и передаётся сюда.
    [RelayCommand]
    private async Task Paste(string? clipboardText)
    {
        var domain = DomainNormalizer.ParseUrlToDomain(clipboardText ?? "");
        if (domain is null) return;
        try
        {
            foreach (var entry in DomainNormalizer.PasteEntries(domain))
                if (!_all.Contains(entry, StringComparer.OrdinalIgnoreCase))
                    await _exclusions.AddAsync(entry);
            await ReloadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    // Путь выбирает View через файловый диалог. Экспорт синхронный (_store.Export),
    // сигнатура Task оставлена ради единообразия с ImportAsync и вызывающего кода.
    public Task ExportAsync(string path)
    {
        try { _store.Export(path, _all); }
        catch (Exception ex) { Error = ex.Message; }
        return Task.CompletedTask;
    }

    public async Task ImportAsync(string path)
    {
        try
        {
            foreach (var d in _store.Import(path))
                if (!_all.Contains(d, StringComparer.OrdinalIgnoreCase))
                    await _exclusions.AddAsync(d);
            await ReloadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
