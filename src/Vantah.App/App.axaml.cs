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
using Vantah.App.Notifications;
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
using Vantah.Core.Notifications;
using Vantah.Core.Settings;
using Vantah.Core.Startup;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Update;
using Vantah.Core.Vpn;

namespace Vantah.App;

public partial class App : Application
{
    private readonly StartupOptions _options;
    private readonly SingleInstance? _instance;
    private readonly bool _unguarded;

    /// <summary>Конструктор для дизайнера XAML и headless-тестов: обычный запуск с окном.</summary>
    public App() : this(StartupOptions.Default, null)
    {
    }

    /// <param name="instance">Замок единственного экземпляра; по сокету из него второй запуск
    /// просит показать окно. null — приложение подняли мимо Program (дизайнер, тесты) либо замок
    /// оказался недоступен.</param>
    /// <param name="unguarded">Замок взять не удалось — запись об этом уходит в лог приложения.</param>
    public App(StartupOptions options, SingleInstance? instance, bool unguarded = false)
    {
        _options = options;
        _instance = instance;
        _unguarded = unguarded;
    }

    // Сервис подписан на AppStateStore и обязан пережить метод инициализации.
    private SectionReloader? _sectionReloader;

    // Лог приложения и его настройка: тумблер в настройках включает запись на лету, поэтому
    // оба экземпляра живут всю сессию.
    private FileAppLog? _appLog;
    private LoggingStore? _loggingStore;

    // Уведомления об обрыве держат своё D-Bus-соединение — на выходе его надо закрыть.
    private DBusDesktopNotifier? _notifier;

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

            // Лог поднимаем до первого вызова CLI, иначе старт в него не попадёт.
            _loggingStore = new LoggingStore();
            _appLog = new FileAppLog { Enabled = _loggingStore.Load() };
            _appLog.Write($"старт: Vantah {AboutViewModel.CurrentVersion}, CLI {cliOptions.Executable}");
            if (_unguarded) _appLog.Write("замок единственного экземпляра недоступен — работаем без защиты от дубля");

            var runner = new CliRunner(cliOptions.Executable, _appLog);
            var vpn = new VpnService(runner, _appLog);

            // Процессы CLI ищем сканом системы, а не учётом собственных детей: туннель CLI
            // демонизирует через «sudo -b», нашим ребёнком он не становится, и в реестре
            // собственных запусков были видны лишь мгновенные «status» — они и мигали.
            var processes = new SystemProcessMonitor(
                new ProcFsProcessSource(cliOptions.Executable),
                new PosixProcessKiller(cliOptions.KillCommand));
            processes.StartPolling(TimeSpan.FromSeconds(2));
            var traffic = new TrafficMonitor(new SysfsTrafficReader(), new SocksTrafficReader());
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
            // Плашки «связь потеряна/восстановлена». Решение принимает координатор по ответам
            // CLI, а не по таймингу: команды пользователя он помечает сам.
            var notificationsStore = new NotificationsStore();
            _notifier = new DBusDesktopNotifier();
            var alerts = new ConnectionAlertService(_notifier, notificationsStore);
            var coordinator = new VpnCoordinator(vpn, traffic, store, history, ipVersionStore, auth, lastLocationStore, killSwitchStore, _appLog, link: alerts);
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
                autoConnectStore, autostart, appUpdates, killSwitchStore, cliUpdater, transparency,
                _loggingStore, _appLog, notificationsStore);

            var license = new LicenseViewModel(vpn, auth, coordinator);
            var about = new AboutViewModel(vpn);

            // Без активного VPN чтения CLI нередко не проходят: раздел, который не прочитался,
            // перечитает себя сам, когда туннель поднимется или появится сам CLI. Ссылку держим
            // в поле — сервис подписан на стор и должен жить всю сессию. license и about идут
            // первыми: их чтения быстрые, а остальные без VPN могут висеть до таймаута.
            _sectionReloader = new SectionReloader(store, [license, about, locations, domains, configVm]);

            var mainVm = new MainWindowViewModel(
                new StatusViewModel(coordinator, store, logReader,
                    new HistoryViewModel(coordinator, store), ipVersionStore),
                locations,
                domains,
                license,
                about,
                new ProcessesViewModel(processes),
                configVm,
                login,
                store, updateBanner);

            var window = new MainWindow { DataContext = mainVm };
            mainWindowRef = window;
            // Окно открывает сам лайфтайм — то, что лежит в MainWindow. Со стартом в трей
            // показывать нечего, поэтому при --minimized не назначаем его вовсе.
            if (!_options.Minimized) desktop.MainWindow = window;

            // Второй запуск просит показать окно по сокету. Колбэк приходит с потока пула,
            // а трогать окно можно только с UI-потока.
            _instance?.StartListening(() =>
                _ = UiThread.RunAsync(() => { window.Show(); window.Activate(); }));
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

            // Системный трей + сворачивание окна вместо выхода. Иконки цветные (серый /
            // янтарный / зелёный) — знак среднего тона читается и на светлой, и на тёмной
            // панели, поэтому подстройка под тему трею не нужна.
            _ = new TrayIconController(coordinator, store, favorites, window, new TrayIconSet());

            // Прятаться можно только там, где трей действительно есть: без watcher'а (GNOME без
            // расширения AppIndicator) спрятанное приложение исчезло бы совсем, без единого окна.
            if (_options.Minimized) _ = ShowIfTrayMissingAsync(window);

            desktop.Exit += (_, _) => _notifier?.Dispose();

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

    // Старт свёрнутым: окна нет, всё управление — в трее. Если трея нет, показываем окно,
    // несмотря на флаг. Проверка асинхронная, поэтому в таком случае окно появляется с
    // задержкой в доли секунды — лучше, чем невидимое приложение.
    private static async Task ShowIfTrayMissingAsync(Window window)
    {
        if (await StatusNotifierWatcherProbe.IsPresentAsync()) return;
        await UiThread.RunAsync(() => { window.Show(); window.Activate(); });
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
            if (info is null || updates.IsDismissed(info)) return;
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
