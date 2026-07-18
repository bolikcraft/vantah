using System;
using System.Globalization;
using System.Threading.Tasks;
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
using Vantah.Core.Auth;
using Vantah.Core.Cli;
using Vantah.Core.Config;
using Vantah.Core.Exclusions;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Update;
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
            var runner = new CliRunner(cliOptions.Executable);
            var vpn = new VpnService(runner);

            // Процессы CLI ищем сканом системы, а не учётом собственных детей: туннель CLI
            // демонизирует через «sudo -b», нашим ребёнком он не становится, и в реестре
            // собственных запусков были видны лишь мгновенные «status» — они и мигали.
            var processes = new SystemProcessMonitor(
                new ProcFsProcessSource(cliOptions.Executable),
                new PosixProcessKiller(cliOptions.KillCommand));
            processes.StartPolling(TimeSpan.FromSeconds(2));
            var traffic = new TrafficMonitor(new SysfsTrafficReader());
            // Активная сессия живёт в отдельном файле и переживает перезапуск: закрытие Vantah
            // не рвёт VPN, поэтому на выходе сессию НЕ финализируем — на следующем старте её
            // подхватит трекер (тот же город — продолжаем, другой/нет — закрываем по heartbeat).
            var historyStore = new ConnectionHistoryStore();
            var activeStore = new ActiveSessionStore();
            var history = new ConnectionHistoryTracker(historyStore, activeStore);
            var ipVersionStore = new IpVersionStore();
            // CliRunner реализует и ICliRunner, и IInteractiveCliRunner: обычные команды и
            // интерактивный login идут через один и тот же процесс-раннер.
            var auth = new AuthService(runner, runner);
            var coordinator = new VpnCoordinator(vpn, traffic, store, history, ipVersionStore, auth);
            var favorites = new FavoritesStore();
            var exclusionsStore = new ExclusionsStore();
            var exclusions = new ExclusionsService(runner, exclusionsStore);
            var logReader = new VpnLogReader();
            // Настройки самого adguardvpn-cli (config show / set-*) — не путать с `config` выше,
            // это INI-конфиг Vantah (~/.config/vantah/vantah.conf).
            var vpnConfig = new ConfigService(runner);
            var updateChecker = new UpdateChecker(runner);
            var logExporter = new LogExporter(runner);

            // Пикер папки для выгрузки логов работает через StorageProvider окна, а окно
            // создаётся ниже — вьюмодели передаём отложенную ссылку, замыкание её увидит позже.
            Window? mainWindowRef = null;

            var login = new LoginViewModel(auth, coordinator);

            var mainVm = new MainWindowViewModel(
                new StatusViewModel(coordinator, store, logReader,
                    new HistoryViewModel(coordinator, store), ipVersionStore),
                new LocationsViewModel(vpn, coordinator, favorites, store),
                new DomainsViewModel(exclusions, exclusionsStore, store),
                new LicenseViewModel(vpn),
                new AboutViewModel(vpn),
                new ProcessesViewModel(processes),
                new ConfigViewModel(
                    vpnConfig, store, languageStore, updateChecker, logExporter,
                    () => PickLogFolderAsync(mainWindowRef)),
                login,
                auth, coordinator, store);

            var window = new MainWindow { DataContext = mainVm };
            mainWindowRef = window;
            desktop.MainWindow = window;
            // Ссылку авторизации открываем системным браузером через Launcher окна.
            login.BrowserOpener = url => window.Launcher.LaunchUriAsync(new Uri(url));

            // Системный трей + сворачивание окна вместо выхода. Иконки цветные (серый /
            // янтарный / зелёный) — знак среднего тона читается и на светлой, и на тёмной
            // панели, поэтому подстройка под тему трею не нужна.
            _ = new TrayIconController(coordinator, store, favorites, window, new TrayIconSet());

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

    // Папка для выгрузки логов — через системный диалог окна; без окна (или без поддержки
    // выбора папки) выгрузку не предлагаем, ExportLogsCommand просто ничего не сделает.
    private static async Task<string?> PickLogFolderAsync(Window? owner)
    {
        if (owner?.StorageProvider is not { CanPickFolder: true } sp) return null;
        var folders = await sp.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = Localizer.Instance[LocKeys.Settings_ExportLogs],
            AllowMultiple = false,
        });
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
