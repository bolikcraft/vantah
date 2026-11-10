using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.Core.Update;

namespace Vantah.App.ViewModels;

/// <summary>
/// Плашка «доступна новая версия AdGuard VPN» под плашкой обновлений Vantah. Кнопка запускает
/// `update -y`; крестик прячет конкретную версию навсегда (кроме показа без версии).
/// </summary>
public partial class CliUpdateBannerViewModel : ObservableObject
{
    private readonly ICliUpdater _updater;
    private readonly CliUpdateStore _store;
    private string? _version;

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool _isUpdating;
    [ObservableProperty] private bool _isUpdated;

    public CliUpdateBannerViewModel(ICliUpdater updater, CliUpdateStore store)
    {
        _updater = updater;
        _store = store;
        // Текст собран в поле — смену языка обрабатываем руками, см. UpdateBannerViewModel.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(UpdateText);
    }

    /// <summary>Показать плашку. null — парсер не нашёл версию в выводе CLI. Вызывать на UI-потоке.</summary>
    public void Show(string? version)
    {
        _version = version;
        IsUpdated = false;
        UpdateText();
        IsVisible = true;
    }

    /// <summary>Спрятать плашку: проверка снова отвечает «актуально».</summary>
    public void Hide() => IsVisible = false;

    private void UpdateText()
    {
        if (IsUpdated)
        {
            Text = Localizer.Instance[LocKeys.Settings_UpdateDone];
            return;
        }
        Text = _version is null
            ? Localizer.Instance[LocKeys.Update_CliAvailable]
            : Localizer.Instance.Format(LocKeys.Update_CliAvailableFormat, _version);
    }

    // Кнопка «Обновить сейчас». Вывод CLI при ошибке — как есть (английский, не переводим).
    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        if (IsUpdating) return;
        IsUpdating = true;
        try
        {
            var r = await _updater.UpdateAsync();
            if (r.Outcome == UpdateOutcome.Failed)
            {
                Text = string.IsNullOrWhiteSpace(r.Output) ? "update failed" : r.Output;
                return;
            }
            IsUpdated = true;
            UpdateText();
        }
        catch (Exception ex)
        {
            Text = UiText.From(ex).Text ?? "";
        }
        finally
        {
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private void Dismiss()
    {
        if (_version is { } v) _store.Update(s => s with { DismissedVersion = v });
        IsVisible = false;
    }
}
