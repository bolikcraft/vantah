using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Vantah.App.Localization;
using Vantah.App.ViewModels;

namespace Vantah.App.Views;

public partial class MainWindow : Window
{
    private readonly DialogHost _dialogs = new();

    public MainWindow() => InitializeComponent();

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnProcessesClick(object? sender, RoutedEventArgs e) =>
        Open("processes", LocKeys.Menu_Processes, vm => new ProcessesView { DataContext = vm.Processes });

    private void OnSettingsClick(object? sender, RoutedEventArgs e) =>
        Open("settings", LocKeys.Menu_Settings, vm => new ConfigView { DataContext = vm.Config });

    private void OnLicenseClick(object? sender, RoutedEventArgs e) =>
        Open("license", LocKeys.Menu_License, vm => new LicenseView { DataContext = vm.License });

    private void OnAboutClick(object? sender, RoutedEventArgs e) =>
        Open("about", LocKeys.Menu_About, vm => new AboutView { DataContext = vm.About });

    /// <summary>
    /// Открытие откладываем на следующий такт диспетчера: Avalonia поднимает Click ДО того, как
    /// закроет MenuFlyout, и окно, показанное прямо из обработчика, всплывает поверх ещё открытого
    /// попапа — а закрытие попапа следом возвращает фокус главному окну. Post показывает окно, когда
    /// меню уже свёрнуто. Тестом эта защита не покрыта: headless-ввод сам прокручивает диспетчер,
    /// поэтому снятие Post ничего там не меняет (см. MainWindowTests: клик через раскрытый флайаут).
    /// Вьюмодель берём из DataContext ровно один раз — в момент клика: фабрика контента выполняется
    /// позже и лениво (только при первом открытии окна), и читать DataContext оттуда нельзя.
    /// </summary>
    private void Open(string key, string titleKey, Func<MainWindowViewModel, Control> createContent)
    {
        if (Vm is not { } vm) return;

        // Заголовок — фабрикой: окно живёт до конца сессии и обязано пережить смену языка.
        // Многоточие в пункте меню обещает диалог; в заголовке самого окна оно лишнее.
        Dispatcher.UIThread.Post(() =>
            _dialogs.Open(
                key,
                () => Localizer.Instance[titleKey].TrimEnd('…'),
                () => createContent(vm),
                this));
    }
}
