using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.Core.Update;

namespace Vantah.App.ViewModels;

/// <summary>
/// Плашка «доступна новая версия Vantah» в верхней части главного окна. Скрыта, пока проверка
/// не нашла обновление; крестик прячет конкретную версию навсегда.
/// </summary>
public partial class UpdateBannerViewModel : ObservableObject
{
    private readonly AppUpdateService _updates;
    private AppUpdateInfo? _info;

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private string _text = "";

    public UpdateBannerViewModel(AppUpdateService updates)
    {
        _updates = updates;
        // Текст собран в поле, поэтому смену языка обрабатываем руками — привязка-индексатор
        // сюда не дотянется.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(UpdateText);
    }

    /// <summary>Открытие ссылки системным браузером; ставит App.axaml.cs (Launcher окна).</summary>
    public Func<string, Task>? BrowserOpener { get; set; }

    /// <summary>Показать плашку. Вызывать на UI-потоке.</summary>
    public void Show(AppUpdateInfo info)
    {
        _info = info;
        UpdateText();
        IsVisible = true;
    }

    private void UpdateText()
    {
        if (_info is null) return;
        Text = Localizer.Instance.Format(LocKeys.Update_Available, _info.Version.TrimStart('v', 'V'));
    }

    [RelayCommand]
    private async Task OpenReleaseAsync()
    {
        if (_info is { } info && BrowserOpener is { } open) await open(info.ReleaseUrl);
    }

    [RelayCommand]
    private void Dismiss()
    {
        if (_info is { } info) _updates.Dismiss(info.Version);
        IsVisible = false;
    }
}
