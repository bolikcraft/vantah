using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tray;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Cli;
using Vantah.Core.Config;
using Vantah.Core.Exclusions;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
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
            // Язык ставим до создания вьюмоделей: часть строк собирается в их конструкторах.
            var languageStore = new LanguageStore();
            Localizer.Instance.SetLanguage(
                CultureSelector.Resolve(languageStore.Load(), CultureInfo.CurrentUICulture));

            var store = new AppStateStore();

            // Путь к CLI и команда убийства настраиваются: ~/.config/vantah/vantah.conf, затем env,
            // затем дефолт «adguardvpn-cli» из PATH.
            var cliOptions = CliOptionsResolver.Resolve(
                IniConfig.Load(VantahPaths.ConfigFile),
                Environment.GetEnvironmentVariable);
            var runner = new CliRunner(cliOptions.Executable, new PosixProcessKiller(cliOptions.KillCommand));
            var vpn = new VpnService(runner);
            var traffic = new TrafficMonitor(new SysfsTrafficReader());
            var historyStore = new ConnectionHistoryStore();
            var history = new ConnectionHistoryTracker(historyStore);
            var coordinator = new VpnCoordinator(vpn, traffic, store, history);
            var favorites = new FavoritesStore();
            var exclusionsStore = new ExclusionsStore();
            var exclusions = new ExclusionsService(runner, exclusionsStore);
            var logReader = new VpnLogReader();

            var mainVm = new MainWindowViewModel(
                new StatusViewModel(coordinator, store, logReader, new HistoryViewModel(coordinator, store)),
                new LocationsViewModel(vpn, coordinator, favorites, store),
                new DomainsViewModel(exclusions, exclusionsStore, store),
                new LicenseViewModel(vpn),
                new AboutViewModel(vpn),
                new ProcessesViewModel(runner), // тот же CliRunner, что исполняет команды, — он же IProcessMonitor
                languageStore);

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
