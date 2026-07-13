using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.App.Localization;
using Vantah.Core.Localization;

namespace Vantah.App.ViewModels;

/// <summary>Язык интерфейса в выпадающем списке. Display — самоназвание, не переводится.</summary>
public sealed record LanguageOption(string Code, string Display);

public partial class MainWindowViewModel : ObservableObject
{
    private readonly LanguageStore _languageStore;

    public StatusViewModel Status { get; }
    public LocationsViewModel Locations { get; }
    public DomainsViewModel Domains { get; }
    public LicenseViewModel License { get; }
    public AboutViewModel About { get; }
    public ProcessesViewModel Processes { get; }
    public ConfigViewModel Config { get; }

    /// <summary>Языки интерфейса. Название каждого — на нём самом, поэтому не переводится.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } = new[]
    {
        new LanguageOption("ru", "Русский"),
        new LanguageOption("en", "English"),
    };

    [ObservableProperty] private LanguageOption _selectedLanguage;

    // Индекс активной вкладки (Статус=0, Локации=1, Домены=2, Лицензия=3, Процессы=4, О программе=5,
    // Настройки=6) — двусторонняя привязка к TabControl; на индексы завязано меню трея,
    // поэтому новые вкладки добавляем в конец.
    [ObservableProperty] private int _selectedTab;

    public MainWindowViewModel(
        StatusViewModel status,
        LocationsViewModel locations,
        DomainsViewModel domains,
        LicenseViewModel license,
        AboutViewModel about,
        ProcessesViewModel processes,
        ConfigViewModel config,
        LanguageStore languageStore)
    {
        Status = status;
        Locations = locations;
        Domains = domains;
        License = license;
        About = about;
        Processes = processes;
        Config = config;
        _languageStore = languageStore;

        // Пишем в backing-поле, а не в свойство: стартовый язык — не выбор пользователя,
        // сохранять его в ~/.config/vantah/language незачем.
        var current = Localizer.Instance.Language;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == current) ?? Languages[0];
    }

    // Выбор языка в UI: переключаем на лету и запоминаем на следующий запуск.
    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        Localizer.Instance.SetLanguage(value.Code);
        _languageStore.Save(value.Code);
    }
}
