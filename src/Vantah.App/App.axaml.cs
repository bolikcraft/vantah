using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
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
            var config = IniConfig.Load(VantahPaths.ConfigFile);

            // Путь к CLI и команда убийства настраиваются: ~/.config/vantah/vantah.conf, затем env,
            // затем дефолт «adguardvpn-cli» из PATH.
            var cliOptions = CliOptionsResolver.Resolve(config, Environment.GetEnvironmentVariable);
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

            // Комплект иконок трея: панель растровую иконку не перекрашивает, поэтому
            // цвет знака выбираем сами — по теме приложения, с переопределением в vantah.conf.
            var configuredPolarity = config.Get(TrayIconResolver.PolarityKey);
            var polarity = ResolveTrayPolarity(configuredPolarity);

            // Системный трей + сворачивание окна вместо выхода.
            var tray = new TrayIconController(coordinator, store, favorites, window, new TrayIconSet(polarity));

            // RequestedThemeVariant=Default → тема приложения следует за системной, а трей живёт
            // сутками и переживает переключение день→ночь. Пересобираем комплект на лету, иначе
            // после смены темы в панели остаётся почти невидимый знак — до перезапуска.
            ActualThemeVariantChanged += (_, _) =>
            {
                var next = ResolveTrayPolarity(configuredPolarity);
                // Явный tray_icon=light|dark даёт ту же полярность при любой теме, поэтому
                // отдельной проверки «не закреплено ли» не нужно: сравнение само её поглощает.
                // Заодно не дёргаем трей на сменах темы, не переворачивающих светлое/тёмное.
                if (next == polarity) return;

                polarity = next;
                tray.UseIcons(new TrayIconSet(polarity));
            };

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

    private TrayIconPolarity ResolveTrayPolarity(string? configured) =>
        TrayIconResolver.ResolvePolarity(configured, appThemeIsDark: ActualThemeVariant == ThemeVariant.Dark);
}
