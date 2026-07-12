using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Vantah.App.Services;
using Vantah.App.Tray;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Cli;
using Vantah.Core.Favorites;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;

namespace Vantah.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new AppStateStore();
            var runner = new CliRunner();
            var vpn = new VpnService(runner);
            var traffic = new TrafficMonitor(new SysfsTrafficReader());
            var coordinator = new VpnCoordinator(vpn, traffic, store);
            var favorites = new FavoritesStore();

            var mainVm = new MainWindowViewModel(
                new StatusViewModel(coordinator, store),
                new LocationsViewModel(vpn, coordinator, favorites, store));

            var window = new MainWindow { DataContext = mainVm };
            desktop.MainWindow = window;

            // Системный трей + сворачивание окна вместо выхода.
            _ = new TrayIconController(coordinator, store, favorites, window);
            window.Closing += (_, e) =>
            {
                e.Cancel = true;
                window.Hide();
            };
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Таймер-опрос статуса/трафика.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += async (_, _) => await coordinator.PollOnceAsync();
            timer.Start();
            _ = coordinator.PollOnceAsync(); // первый опрос сразу
        }

        base.OnFrameworkInitializationCompleted();
    }
}
