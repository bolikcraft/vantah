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
        Open("processes", LocKeys.Menu_Processes, () => new ProcessesView { DataContext = Vm!.Processes });

    private void OnSettingsClick(object? sender, RoutedEventArgs e) =>
        Open("settings", LocKeys.Menu_Settings, () => new ConfigView { DataContext = Vm!.Config });

    private void OnLicenseClick(object? sender, RoutedEventArgs e) =>
        Open("license", LocKeys.Menu_License, () => new LicenseView { DataContext = Vm!.License });

    private void OnAboutClick(object? sender, RoutedEventArgs e) =>
        Open("about", LocKeys.Menu_About, () => new AboutView { DataContext = Vm!.About });

    /// <summary>
    /// Открытие откладываем на следующий такт диспетчера: MenuFlyout закрывается прямо в обработчике
    /// Click, и элемент, на котором мы стоим, к этому моменту уже отцеплен от дерева.
    /// </summary>
    private void Open(string key, string titleKey, Func<Control> createContent)
    {
        if (Vm is null) return;

        Dispatcher.UIThread.Post(() =>
        {
            // Многоточие в пункте меню обещает диалог; в заголовке самого окна оно лишнее.
            var title = Localizer.Instance[titleKey].TrimEnd('…');
            _dialogs.Open(key, title, createContent, this);
        });
    }
}
