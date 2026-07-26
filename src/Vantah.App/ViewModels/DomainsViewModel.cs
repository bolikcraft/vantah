using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.ViewModels;

public partial class DomainsViewModel : ErrorAwareViewModel
{
    private readonly IExclusionsService _exclusions;
    private readonly ExclusionsStore _store;
    private readonly AppStateStore _appState;
    private readonly List<string> _all = new();

    private SiteExclusionMode _mode = SiteExclusionMode.General;
    private bool _switchingMode;   // защита от реентранта при программной установке радио
    private bool _errorFromInput;  // текущая ошибка — про введённую строку, а не про CLI
    private bool _loadFailed;      // последняя загрузка списка упала (таймаут/ошибка CLI)
    private bool _loaded;          // список хоть раз успешно загрузился — на экране есть что показывать

    [ObservableProperty] private string _query = "";  // одно поле: ввод домена и фильтр списка
    [ObservableProperty] private bool _isGeneral = true;
    [ObservableProperty] private bool _isSelective;
    [ObservableProperty] private bool _isBusy;

    // Состояние ПЕРВОЙ загрузки списка: до ответа `site-exclusions show` показывать пустой
    // список и рабочие кнопки нельзя — экран читается как «исключений нет».
    [ObservableProperty] private bool _isLoading = true;
    // Причина сбоя, а не готовая строка: после смены языка сообщение пересобирается.
    private UiText _loadErrorValue;
    [ObservableProperty] private string? _loadError;

    /// <summary>Список получен — можно показывать режимы, поле ввода, кнопки и сам перечень.</summary>
    public bool IsLoaded => !IsLoading && LoadError is null;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsLoaded));
    partial void OnLoadErrorChanged(string? value) => OnPropertyChanged(nameof(IsLoaded));

    /// <summary>Строки-заглушки «скелетона»: ширины намеренно разные, иначе ровный столбик
    /// плиток читается как таблица с данными. Считаются формулой, а не Random — чтобы кадр
    /// экрана был воспроизводимым.</summary>
    public IReadOnlyList<double> SkeletonRows { get; } =
        Enumerable.Range(0, 9).Select(i => 96.0 + (i * 53 % 140)).ToList();

    public ObservableCollection<DomainItemViewModel> Items { get; } = new();

    /// <summary>Текущая (пере)загрузка списка исключений — чтобы её можно было дождаться в тестах.</summary>
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    public DomainsViewModel(IExclusionsService exclusions, ExclusionsStore store, AppStateStore appState)
    {
        _exclusions = exclusions;
        _store = store;
        _appState = appState;
        // ErrorAwareViewModel пересобирает при смене языка только Error; сообщение о сбое
        // загрузки живёт отдельно, поэтому переводим его сами — иначе оно останется на языке,
        // который был в момент сбоя.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(() => LoadError = _loadErrorValue.Text);
        LoadTask = ReloadAsync();
    }

    partial void OnQueryChanged(string value)
    {
        // Правка поля — сама по себе ответ на жалобу («это не похоже на домен»): держать её
        // на экране до следующего «Добавить» незачем. Гасим только ЭТУ жалобу: поле работает
        // ещё и фильтром списка, а ошибка удаления/очистки/импорта к набору текста отношения
        // не имеет — потерять её при поиске строки нельзя.
        if (_errorFromInput) { ClearError(); _errorFromInput = false; }
        ApplyFilter();
    }

    partial void OnIsGeneralChanged(bool value)
    {
        if (value && !_switchingMode) _ = SwitchModeAsync(SiteExclusionMode.General);
    }

    partial void OnIsSelectiveChanged(bool value)
    {
        if (value && !_switchingMode) _ = SwitchModeAsync(SiteExclusionMode.Selective);
    }

    /// <summary>Повторная загрузка списка (кнопка «Обновить»): без неё сбой CLI при старте
    /// оставляет вкладку пустой навсегда — перезапуск приложения был единственным выходом.</summary>
    [RelayCommand]
    private Task Reload() => LoadTask = ReloadAsync();

    /// <summary>Возврат на вкладку «Домены» после неудачной загрузки — пробуем ещё раз.
    /// Успешно загруженный (пусть и пустой) список не перечитываем: CLI дёргать зря незачем.</summary>
    public void ReloadIfFailed()
    {
        if (_loadFailed) LoadTask = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        // Скелетон уместен, только пока показывать нечего. ReloadAsync дёргается после каждого
        // добавления/удаления/импорта, и схлопывать уже показанный список в заглушки нельзя —
        // экран мигал бы на каждое действие. Признак «первая ли это загрузка» снимаем до await.
        var first = !_loaded;
        try
        {
            IsBusy = true; ClearError();
            if (first) { IsLoading = true; _loadErrorValue = UiText.None; LoadError = null; }
            var snap = await _exclusions.GetAsync();
            // Продолжение после await может оказаться на потоке пула (реальный CliRunner ждёт
            // внешний процесс). Items и радио-свойства привязаны к UI, поэтому их правку выполняем
            // строго на UI-потоке — иначе Avalonia роняет cross-thread исключение и вкладка молча
            // остаётся пустой (headless-тесты этого не ловят: там продолжение всегда на UI-потоке).
            await UiThread.RunAsync(() =>
            {
                _mode = snap.Mode;
                _switchingMode = true;
                IsGeneral = snap.Mode == SiteExclusionMode.General;
                IsSelective = snap.Mode == SiteExclusionMode.Selective;
                _switchingMode = false;

                _all.Clear();
                _all.AddRange(snap.Domains);
                ApplyFilter();
                _appState.Set(s => s with { ExclusionsCount = _all.Count, ExclusionsMode = _mode });
                _loaded = true;
                _loadFailed = false;
                _loadErrorValue = UiText.None;
                LoadError = null;
            });
        }
        catch (Exception ex)
        {
            await UiThread.RunAsync(() =>
            {
                var error = UiText.From(ex);
                _loadFailed = true;
                // Список уже на экране — это сбой отдельного действия, показываем его в общем
                // баннере и оставляем данные. Если же показывать нечего, сбой описывает
                // состояние всей вкладки: вместо пустоты — текст ошибки и «Обновить».
                if (_loaded) SetError(error);
                else { _loadErrorValue = error; LoadError = error.Text; }
            });
        }
        finally
        {
            await UiThread.RunAsync(() =>
            {
                IsBusy = false;
                if (first) IsLoading = false;
            });
        }
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
            IsBusy = true; ClearError();
            await _exclusions.SetModeAsync(_mode, target, _all.ToList());
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            // Переключение упало: CLI/_mode остались на прежнем режиме — вернуть радио к нему,
            // иначе визуально «залипнет» на целевом, а повторный клик того же радио ничего не запустит.
            SetError(ex);
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

        // Опечатка вроде «exmaple» или «not a domain» иначе молча уедет в CLI и вернётся
        // мусорной записью в списке или невнятной ошибкой. Проверяем тем же предикатом,
        // которым Normalize фильтрует импорт файла и вывод CLI. Ввод не затираем — его правят.
        if (!DomainNormalizer.IsAcceptableDomain(domain))
        {
            SetError(LocKeys.Domains_InvalidEntry);
            _errorFromInput = true;
            return;
        }

        try { ClearError(); await _exclusions.AddAsync(domain); Query = ""; await ReloadAsync(); }
        catch (Exception ex) { SetError(ex); }
    }

    [RelayCommand]
    private async Task Remove(DomainItemViewModel item)
    {
        try { await _exclusions.RemoveAsync(item.Domain); await ReloadAsync(); }
        catch (Exception ex) { SetError(ex); }
    }

    // Полную очистку с подтверждением инициирует View (диалог), сюда приходит уже подтверждённый вызов.
    [RelayCommand]
    private async Task Clear()
    {
        try { foreach (var d in _all.ToList()) await _exclusions.RemoveAsync(d); await ReloadAsync(); }
        catch (Exception ex) { SetError(ex); }
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
        catch (Exception ex) { SetError(ex); }
    }

    // Путь выбирает View через файловый диалог. Экспорт синхронный (_store.Export),
    // сигнатура Task оставлена ради единообразия с ImportAsync и вызывающего кода.
    public Task ExportAsync(string path)
    {
        try { _store.Export(path, _all); }
        catch (Exception ex) { SetError(ex); }
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
        catch (Exception ex) { SetError(ex); }
    }
}
