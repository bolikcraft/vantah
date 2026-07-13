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
    /// Открытие откладываем на следующий такт диспетчера: MenuFlyout закрывается прямо в обработчике
    /// Click, и элемент, на котором мы стоим, к этому моменту уже отцеплен от дерева. Вьюмодель при
    /// этом берём из DataContext ровно один раз — в момент клика: фабрика контента выполняется
    /// позже и лениво (только при первом открытии окна), и читать DataContext оттуда нельзя.
    /// </summary>
    private void Open(string key, string titleKey, Func<MainWindowViewModel, Control> createContent)
    {
        if (Vm is not { } vm) return;

        Dispatcher.UIThread.Post(() =>
        {
            // Многоточие в пункте меню обещает диалог; в заголовке самого окна оно лишнее.
            var title = Localizer.Instance[titleKey].TrimEnd('…');
            _dialogs.Open(key, title, () => createContent(vm), this);
        });
    }
}
