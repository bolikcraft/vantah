using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tray;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Appearance;
using Vantah.Core.Auth;
using Vantah.Core.Autostart;
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
    // Сервис подписан на AppStateStore и обязан пережить метод инициализации.
    private SectionReloader? _sectionReloader;

    // Наблюдатель за трей-watcher'ом владеет собственным D-Bus-соединением — тоже держим ссылку.
    private StatusNotifierWatcherMonitor? _trayWatcher;

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
            var killSwitchStore = new KillSwitchStore();
            // Прозрачность применяем до создания окон: иначе первый кадр будет с дефолтной кистью,
            // и окно на мгновение мигнёт другой прозрачностью.
            var transparency = new WindowTransparency(new WindowOpacityStore());
            transparency.Apply();
            // CliRunner реализует и ICliRunner, и IInteractiveCliRunner: обычные команды и
            // интерактивный login идут через один и тот же процесс-раннер.
            var auth = new AuthService(runner, runner);
            var lastLocationStore = new LastLocationStore();
            var coordinator = new VpnCoordinator(vpn, traffic, store, history, ipVersionStore, auth, lastLocationStore, killSwitchStore);
            var favorites = new FavoritesStore();
            var exclusionsStore = new ExclusionsStore();
            var exclusions = new ExclusionsService(runner, exclusionsStore);
            var logReader = new VpnLogReader();
            // Настройки самого adguardvpn-cli (config show / set-*) — не путать с `config` выше,
            // это INI-конфиг Vantah (~/.config/vantah/vantah.conf).
            var vpnConfig = new ConfigService(runner);
            var updateChecker = new UpdateChecker(runner);
            var cliUpdater = new CliUpdater(runner);
            var logExporter = new LogExporter(runner);
            var autoConnectStore = new AutoConnectStore();
            var autostart = new AutostartService(
                VantahPaths.AutostartDir,
                Environment.ProcessPath ?? "vantah", "vantah");

            // Проверка обновлений самого Vantah (не путать с updateChecker выше — тот про CLI).
            // Версию берём ту же, что показывает «О программе».
            var appVersion = AboutViewModel.CurrentVersion;
            // Основной источник — GitHub API, запасной — редирект web-морды: API без токена
            // ограничен 60 запросами в час на IP, а наши пользователи сидят за общими адресами
            // VPN и выбирают этот лимит чужим трафиком.
            var appUpdates = new AppUpdateService(
                new FallbackReleaseSource(
                    new GitHubReleaseSource(appVersion),
                    new GitHubRedirectReleaseSource(appVersion)),
                new AppUpdateStore(), appVersion);
            var updateBanner = new UpdateBannerViewModel(appUpdates);

            // Пикер папки для выгрузки логов работает через StorageProvider окна, а окно
            // создаётся ниже — вьюмодели передаём отложенную ссылку, замыкание её увидит позже.
            Window? mainWindowRef = null;

            var login = new LoginViewModel(auth, coordinator);

            var locations = new LocationsViewModel(vpn, coordinator, favorites, store);
            var domains = new DomainsViewModel(exclusions, exclusionsStore, store);
            var configVm = new ConfigViewModel(
                vpnConfig, store, languageStore, updateChecker, logExporter,
                () => PickLogFolderAsync(mainWindowRef),
                autoConnectStore, autostart, appUpdates, killSwitchStore, cliUpdater, transparency);

            // Без активного VPN чтения CLI нередко не проходят: раздел, который не прочитался,
            // перечитает себя сам, когда туннель поднимется. Ссылку держим в поле — сервис
            // подписан на стор и должен жить всю сессию.
            _sectionReloader = new SectionReloader(store, [locations, domains, configVm]);

            var mainVm = new MainWindowViewModel(
                new StatusViewModel(coordinator, store, logReader,
                    new HistoryViewModel(coordinator, store), ipVersionStore),
                locations,
                domains,
                new LicenseViewModel(vpn, auth, coordinator),
                new AboutViewModel(vpn),
                new ProcessesViewModel(processes),
                configVm,
                login,
                store, updateBanner);

            var window = new MainWindow { DataContext = mainVm };
            mainWindowRef = window;
            desktop.MainWindow = window;
            // Ссылку авторизации открываем системным браузером через Launcher окна.
            // Открываем только http/https: страховка на случай изменения парсера ссылки.
            // TryCreate вместо new Uri — кривая ссылка не должна ронять команду баннера
            // обновлений (там нет try/catch, исключение утекло бы в AsyncRelayCommand).
            // Отказ намеренно тихий: механизма логирования в приложении нет, а ссылка
            // из CLI и так проверена регуляркой https?:// в DeviceCodeParser — сюда
            // попадём только при будущем регрессе парсера.
            Task OpenInBrowser(string url) =>
                Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                    ? window.Launcher.LaunchUriAsync(uri)
                    : Task.CompletedTask;

            login.BrowserOpener = OpenInBrowser;
            updateBanner.BrowserOpener = OpenInBrowser;
            // Буфер обмена — тоже от окна (TopLevel.Clipboard), как в DomainsView. На платформе
            // без буфера просто ничего не делаем: ссылку на экране можно выделить руками.
            login.ClipboardWriter = text => window.Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;

            // Проверка обновлений — фоном и без ожидания: старт и автоподключение она не задерживает.
            _ = CheckAppUpdateAsync(appUpdates, updateBanner);

            // Сеть под снятие иконки трея ставим ДО её создания: без неё отмена из петли
            // DBusTrayIconImpl.WatchAsync прилетает на диспетчер и роняет процесс — и при
            // пересоздании иконки, и на обычном выходе через меню трея (rc=134 вместо 0).
            // Только Linux: петля живёт в DBusTrayIconImpl, пересоздание трея нигде больше не
            // запускается, а подписка на UnhandledException диспетчера — вещь глобальная, и
            // ставить её там, где она заведомо ничего не ловит, значит менять поведение зря.
            if (OperatingSystem.IsLinux()) TrayIconGuard.Install();

            // Системный трей + сворачивание окна вместо выхода. Иконки цветные (серый /
            // янтарный / зелёный) — знак среднего тона читается и на светлой, и на тёмной
            // панели, поэтому подстройка под тему трею не нужна.
            var tray = new TrayIconController(coordinator, store, favorites, window, new TrayIconSet());

            // GNOME при блокировке экрана выгружает расширение appindicatorsupport, а на
            // возврате регистрирует наш /StatusNotifierItem раньше, чем Avalonia успевает взять
            // своё well-known имя, — в трее оказываются ДВЕ иконки одного процесса. Обход:
            // дождаться возврата watcher'а и пересоздать трей вместе с D-Bus-соединением.
            // Наблюдение поднимается здесь, а не в конструкторе контроллера, чтобы headless-тесты
            // трея не открывали соединение к настоящей сессионной шине пользователя.
            _trayWatcher = StatusNotifierWatcherMonitor.TryStart(tray.RebuildAsync);

            // На выходе наблюдение закрываем: иначе за приложением остаётся живое
            // D-Bus-соединение, а пересоздание, начатое в середине пятисекундной паузы,
            // доедет до уже разобранного трея.
            desktop.Exit += (_, _) => _trayWatcher?.Dispose();

            // Прячем окно только когда его закрывает пользователь (крестик / Alt+F4). При
            // завершении сеанса причина другая (ApplicationShutdown/OSShutdown), и вето здесь
            // означало бы «отменить выключение»: Avalonia участвует в X11-сессии (XSMP), и отказ
            // закрыть окно уходит менеджеру сеанса как отмена — GNOME тогда не показывает даже
            // диалог подтверждения и машина не выключается.
            window.Closing += (_, e) =>
            {
                if (e.CloseReason != WindowCloseReason.WindowClosing) return;
                e.Cancel = true;
                window.Hide();
            };
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Таймер-опрос статуса/трафика.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (_, _) => _ = SafePollAsync(coordinator);
            // Первичный опрос + автоконнект, затем запускаем периодический опрос — без гонки со стартом.
            _ = InitialConnectAsync(coordinator, store, autoConnectStore, lastLocationStore)
                    .ContinueWith(_ => Dispatcher.UIThread.Post(timer.Start), TaskScheduler.Default);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Автоподключение на старте: сперва опрашиваем реальное состояние CLI (снапшот мог быть
    // устаревшим/пустым), затем решаем по плану — коннектимся, только если залогинены и сейчас
    // отключены; уже поднятый VPN не трогаем.
    private static async Task InitialConnectAsync(
        VpnCoordinator coordinator, AppStateStore store,
        AutoConnectStore autoConnect, LastLocationStore lastLocation)
    {
        await coordinator.PollOnceAsync();
        var action = AutoConnectPlanner.Plan(
            store.Current.Connection, store.Current.LoginState,
            autoConnect.Load(), lastLocation.Load());
        if (action.ShouldConnect)
            await coordinator.ConnectAsync(action.Location, action.Fastest);
    }

    // Тик таймера-опроса — fire-and-forget: необработанное исключение в async void уронило бы
    // UI-поток, поэтому глушим его здесь и просто пропускаем один тик.
    private static async Task SafePollAsync(VpnCoordinator coordinator)
    {
        try { await coordinator.PollOnceAsync(); }
        catch { /* тик опроса не должен ронять UI-поток */ }
    }

    // Проверка новой версии Vantah при старте. Результат кладём в плашку только через
    // Dispatcher: заполнение привязанных свойств из фонового потока в UI не доезжает.
    private static async Task CheckAppUpdateAsync(AppUpdateService updates, UpdateBannerViewModel banner)
    {
        try
        {
            var info = await updates.CheckAsync(DateTimeOffset.UtcNow);
            if (info is null) return;
            Dispatcher.UIThread.Post(() => banner.Show(info));
        }
        catch
        {
            // Проверка обновлений необязательна и не должна ронять старт приложения.
        }
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
