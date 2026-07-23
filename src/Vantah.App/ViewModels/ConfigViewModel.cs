using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.Core.Autostart;
using Vantah.Core.Errors;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Vantah.Core.Update;
using Vantah.Core.Vpn;

namespace Vantah.App.ViewModels;

/// <summary>Язык интерфейса в выпадающем списке. Display — самоназвание, не переводится.</summary>
public sealed record LanguageOption(string Code, string Display);

/// <summary>
/// Вкладка «Настройки». Тумблеры и выпадающие списки применяются сразу, текстовые поля — по кнопке.
/// Форма всегда перерисовывается из ответа CLI, а не из введённого: показываем применённое, а не желаемое.
/// </summary>
public partial class ConfigViewModel : ErrorAwareViewModel
{
    private readonly IConfigService _config;
    private readonly LanguageStore _languageStore;
    private readonly IUpdateChecker _updates;
    private readonly ILogExporter _logs;
    private readonly Func<Task<string?>> _pickFolder;
    private readonly AutoConnectStore _autoConnect;
    private readonly AutostartService _autostart;
    private readonly AppUpdateService? _appUpdates;
    private readonly ICliUpdater? _updater;
    private bool _lastRadioWasFastest;

    // Подавляет авто-применение, пока форму заполняем программно: иначе загрузка конфига
    // тут же отправила бы обратно в CLI всё, что только что из него прочитали.
    private bool _loading;

    public ConfigViewModel(
        IConfigService config,
        AppStateStore store,
        LanguageStore languageStore,
        IUpdateChecker updates,
        ILogExporter logs,
        Func<Task<string?>> pickFolder,
        AutoConnectStore? autoConnect = null,
        AutostartService? autostart = null,
        AppUpdateService? appUpdates = null,
        ICliUpdater? updater = null)
    {
        _config = config;
        _languageStore = languageStore;
        _updates = updates;
        _logs = logs;
        _pickFolder = pickFolder;
        // Опциональны ради старых мест создания вьюмодели (тесты других вкладок), которым
        // автоподключение/автозапуск не интересны: без явных зависимостей — дефолтные пути,
        // как в App.axaml.cs. Тем, кому поведение важно (наш тест), передают свои — на temp.
        _autoConnect = autoConnect ?? new AutoConnectStore();
        _autostart = autostart ?? new AutostartService(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "autostart"),
            Environment.ProcessPath ?? "vantah", "vantah");
        // Проверка обновлений самого Vantah тоже опциональна: без неё тумблер просто
        // показывает значение по умолчанию и никуда не пишет.
        _appUpdates = appUpdates;
        // Установка обновления CLI тоже опциональна: без неё команда «Обновить сейчас»
        // ничего не делает (кнопка живёт только в баннере обновления).
        _updater = updater;

        // Пишем в backing-поле, а не в свойство: стартовый язык — не выбор пользователя,
        // сохранять его в ~/.config/vantah/language незачем.
        var current = Localizer.Instance.Language;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == current) ?? Languages[0];

        // Статус и плашка обновления собираются в коде — после смены языка пересобираем их
        // из запомненных ключей, иначе они висят на прежнем языке до перезапуска.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(RefreshTexts);

        store.Changed += (_, s) => Dispatcher.UIThread.Post(() =>
            IsConnectedWarningVisible = s.Connection == ConnectionState.Connected);
        IsConnectedWarningVisible = store.Current.Connection == ConnectionState.Connected;

        // Автоподключение и автозапуск читаются не из CLI, а из своих локальных хранилищ —
        // грузим их так же под _loading, чтобы инициализация формы не улетела обратно записью.
        _loading = true;
        try
        {
            var mode = _autoConnect.Load();
            AutoConnectEnabled = mode != AutoConnectMode.Off;
            AutoConnectUseFastest = mode == AutoConnectMode.Fastest;
            _lastRadioWasFastest = AutoConnectUseFastest;
            AutostartEnabled = _autostart.IsEnabled();
            CheckAppUpdates = _appUpdates?.Enabled ?? true;
        }
        finally { _loading = false; }

        LoadTask = LoadAsync();
        UpdateCheckTask = CheckForUpdatesAsync();
    }

    /// <summary>
    /// Языки интерфейса. Название каждого — на нём самом, поэтому не переводится. Порядок:
    /// языки авторов первыми, дальше по алфавиту самоназваний. Список должен совпадать с
    /// <see cref="CultureSelector.Supported"/> — это проверяет тест.
    /// </summary>
    public static IReadOnlyList<LanguageOption> AllLanguages { get; } = new[]
    {
        new LanguageOption("ru", "Русский"),
        new LanguageOption("en", "English"),
        new LanguageOption("de", "Deutsch"),
        new LanguageOption("es", "Español"),
        new LanguageOption("fr", "Français"),
        new LanguageOption("id", "Bahasa Indonesia"),
        new LanguageOption("it", "Italiano"),
        new LanguageOption("pl", "Polski"),
        new LanguageOption("pt-BR", "Português (Brasil)"),
        new LanguageOption("tr", "Türkçe"),
        new LanguageOption("uk", "Українська"),
        new LanguageOption("zh-Hans", "简体中文"),
    };

    /// <summary>Тот же список для привязки в XAML (у статического свойства биндинг неудобен).</summary>
    public IReadOnlyList<LanguageOption> Languages => AllLanguages;

    [ObservableProperty] private LanguageOption _selectedLanguage;

    // Выбор языка в UI: переключаем на лету и запоминаем на следующий запуск.
    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        Localizer.Instance.SetLanguage(value.Code);
        _languageStore.Save(value.Code);
    }

    // Токены CLI — техническая номенклатура, одинаковая на всех языках, поэтому не переводятся.
    public IReadOnlyList<string> ProtocolOptions { get; } = ["auto", "http2", "quic"];
    public IReadOnlyList<string> ChannelOptions { get; } = ["release", "beta", "nightly"];
    public IReadOnlyList<string> RoutingOptions { get; } = ["auto", "script", "none"];

    [ObservableProperty] private bool _isSocksMode;
    [ObservableProperty] private string _socksPort = "1080";
    [ObservableProperty] private string _socksHost = "127.0.0.1";
    [ObservableProperty] private string _socksUsername = "";
    [ObservableProperty] private string _socksPassword = "";
    [ObservableProperty] private string _dnsUpstream = "";
    [ObservableProperty] private bool _changeSystemDns;
    [ObservableProperty] private bool _postQuantum;
    [ObservableProperty] private bool _showNotifications;
    [ObservableProperty] private bool _debugLogging;
    [ObservableProperty] private bool _crashReporting;
    [ObservableProperty] private bool _telemetry;
    [ObservableProperty] private bool _showHints;
    [ObservableProperty] private string _interfaceOverride = "";
    [ObservableProperty] private string _selectedProtocol = "auto";
    [ObservableProperty] private string _selectedChannel = "release";
    [ObservableProperty] private string _selectedRouting = "auto";

    [ObservableProperty] private bool _isBusy;
    // Статус («сохранено», путь к логам) и плашка обновления — тоже значения, а не готовые
    // строки: после смены языка пересобираются из тех же ключей (см. RefreshTexts).
    private UiText _statusValue;
    private UiText _updateValue;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private bool _isConnectedWarningVisible;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private string _updateMessage = "";

    [ObservableProperty] private bool _autoConnectEnabled;
    [ObservableProperty] private bool _autoConnectUseFastest;
    [ObservableProperty] private bool _autostartEnabled;
    [ObservableProperty] private bool _checkAppUpdates = true;

    partial void OnAutoConnectEnabledChanged(bool value) => PersistAutoConnect();

    partial void OnAutoConnectUseFastestChanged(bool value)
    {
        if (_loading) return;
        _lastRadioWasFastest = value;
        PersistAutoConnect();
    }

    partial void OnAutostartEnabledChanged(bool value)
    {
        if (_loading) return;
        if (value) _autostart.Enable(); else _autostart.Disable();
    }

    partial void OnCheckAppUpdatesChanged(bool value)
    {
        if (_loading || _appUpdates is null) return;
        _appUpdates.Enabled = value;
    }

    // Единственный источник истины — AutoConnectStore: чекбокс и радиокнопка «сжимаются»
    // в один AutoConnectMode. Выключенный чекбокс — всегда Off, независимо от радиокнопки;
    // включённый — Fastest либо LastUsed (последний выбор радиокнопки, по умолчанию LastUsed).
    private void PersistAutoConnect()
    {
        if (_loading) return;
        var mode = !AutoConnectEnabled
            ? AutoConnectMode.Off
            : (_lastRadioWasFastest ? AutoConnectMode.Fastest : AutoConnectMode.LastUsed);
        _autoConnect.Save(mode);
    }

    /// <summary>Задача авто-проверки обновления — для ожидания в тестах.</summary>
    public Task UpdateCheckTask { get; }

    /// <summary>Текущая (пере)загрузка конфигурации — чтобы её можно было дождаться в тестах.</summary>
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    private async Task LoadAsync()
    {
        try
        {
            var cfg = await _config.GetAsync();
            // Продолжение после await может оказаться на потоке пула (реальный CliRunner ждёт
            // внешний процесс). Apply правит привязанные к UI свойства, поэтому выполняем его
            // строго на UI-потоке — иначе Avalonia роняет cross-thread исключение и форма молча
            // остаётся с дефолтами (headless-тесты этого не ловят: там продолжение всегда на UI-потоке).
            await UiThread.RunAsync(() => Apply(cfg));
        }
        catch (Exception ex)
        {
            await UiThread.RunAsync(() => SetError(ex));
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var s = await _updates.CheckAsync();
            if (s.IsLatest) return;
            await UiThread.RunAsync(() =>
            {
                SetUpdateMessage(string.IsNullOrEmpty(s.LatestVersion)
                    ? UiText.Key(LocKeys.Settings_UpdateAvailable)
                    : UiText.Key(LocKeys.Settings_UpdateAvailableFormat, s.LatestVersion));
                IsUpdateAvailable = true;
            });
        }
        catch { /* проверка обновления не критична — молчим */ }
    }

    private void Apply(VpnConfig c)
    {
        _loading = true;
        try
        {
            IsSocksMode = c.Mode == VpnMode.Socks;
            SocksPort = c.SocksPort.ToString();
            SocksHost = c.SocksHost ?? "";
            SocksUsername = c.SocksUsername ?? "";
            // Дефолтный апстрим CLI показывает фразой «Default (provided by AdGuard VPN)» —
            // в поле ввода ей делать нечего, там должно быть пусто.
            DnsUpstream = c.DnsIsDefault ? "" : c.DnsUpstream;
            ChangeSystemDns = c.ChangeSystemDns;
            PostQuantum = c.PostQuantum;
            ShowNotifications = c.ShowNotifications;
            DebugLogging = c.DebugLogging;
            CrashReporting = c.CrashReporting;
            Telemetry = c.SendAnonymizedUsageData;
            ShowHints = c.ShowHints;
            InterfaceOverride = c.OutboundInterfaceOverride ?? "";
            SelectedProtocol = c.Protocol.ToString().ToLowerInvariant();
            SelectedChannel = c.UpdateChannel.ToString().ToLowerInvariant();
            SelectedRouting = c.TunnelRoutingMode.ToString().ToLowerInvariant();
        }
        finally { _loading = false; }
    }

    partial void OnIsSocksModeChanged(bool value) =>
        AutoApply(() => _config.SetModeAsync(value ? VpnMode.Socks : VpnMode.Tun));

    partial void OnChangeSystemDnsChanged(bool value) =>
        AutoApply(() => _config.SetChangeSystemDnsAsync(value));

    partial void OnPostQuantumChanged(bool value) =>
        AutoApply(() => _config.SetPostQuantumAsync(value));

    partial void OnShowNotificationsChanged(bool value) =>
        AutoApply(() => _config.SetShowNotificationsAsync(value));

    partial void OnDebugLoggingChanged(bool value) =>
        AutoApply(() => _config.SetDebugLoggingAsync(value));

    partial void OnCrashReportingChanged(bool value) =>
        AutoApply(() => _config.SetCrashReportingAsync(value));

    partial void OnTelemetryChanged(bool value) =>
        AutoApply(() => _config.SetTelemetryAsync(value));

    partial void OnShowHintsChanged(bool value) =>
        AutoApply(() => _config.SetShowHintsAsync(value));

    partial void OnSelectedProtocolChanged(string value) =>
        AutoApply(() => _config.SetProtocolAsync(ParseProtocol(value)));

    partial void OnSelectedChannelChanged(string value) =>
        AutoApply(() => _config.SetUpdateChannelAsync(ParseChannel(value)));

    partial void OnSelectedRoutingChanged(string value)
    {
        OnPropertyChanged(nameof(IsRouteScriptAvailable));
        AutoApply(() => _config.SetTunRoutingModeAsync(ParseRouting(value)));
    }

    /// <summary>Route-скрипт имеет смысл только при routing = script.</summary>
    public bool IsRouteScriptAvailable => SelectedRouting == "script";

    [RelayCommand]
    private Task CreateRouteScript() =>
        RunAsyncText(() => _config.CreateRouteScriptAsync());

    private void AutoApply(Func<Task<VpnConfig>> action)
    {
        if (_loading) return;
        _ = RunAsync(action);
    }

    [RelayCommand]
    private Task ApplySocksPort() =>
        int.TryParse(SocksPort, out var p) && p is > 0 and <= 65535
            ? RunAsync(() => _config.SetSocksPortAsync(p))
            : Fail(LocKeys.Settings_ErrorInvalidPort);

    [RelayCommand]
    private Task ApplySocksHost() =>
        string.IsNullOrWhiteSpace(SocksHost)
            ? Fail(LocKeys.Settings_ErrorEmptyHost)
            : RunAsync(() => _config.SetSocksHostAsync(SocksHost.Trim()));

    [RelayCommand]
    private Task ApplySocksAuth() =>
        RunAsync(async () =>
        {
            var saved = await _config.SetSocksUsernameAsync(SocksUsername.Trim());
            // Пустое поле пароля — «меняем только логин», а не «сбросить пароль»: после
            // успешного применения поле очищается, и повторное «Применить» (поправить
            // опечатку в логине) иначе отправило бы в CLI пустой пароль. Сброс
            // аутентификации делает отдельная команда ClearSocksAuth.
            if (SocksPassword.Length > 0)
            {
                saved = await _config.SetSocksPasswordAsync(SocksPassword);
                // Убираем пароль из формы: он уже применён, и незачем держать его на
                // экране и в скриншотах. Гарантией затирания памяти это не является —
                // прежний экземпляр string живёт до сборки мусора (и в undo-стеке TextBox).
                SocksPassword = "";
            }
            return saved;
        });

    [RelayCommand]
    private Task ClearSocksAuth() => RunAsync(() => _config.ClearSocksAuthAsync());

    // Пустое поле — не ошибка, а осознанный сброс: CLI принимает литерал «default»
    // и возвращает разрешение имён на серверы AdGuard.
    [RelayCommand]
    private Task ApplyDns() =>
        string.IsNullOrWhiteSpace(DnsUpstream)
            ? RunAsync(() => _config.ResetDnsAsync())
            : RunAsync(() => _config.SetDnsAsync(DnsUpstream.Trim()));

    // Пресет DNS — известный публичный upstream одним кликом. Показываем его в поле
    // (чтобы было видно применённое) и шлём тем же путём, что и ручной ввод.
    [RelayCommand]
    private Task SelectDnsPreset(string upstream)
    {
        DnsUpstream = upstream;
        return RunAsync(() => _config.SetDnsAsync(upstream));
    }

    // Пустое поле — осознанное отключение override; CLI принимает пустой аргумент.
    [RelayCommand]
    private Task ApplyInterfaceOverride() =>
        RunAsync(() => _config.SetBoundIfOverrideAsync(InterfaceOverride.Trim()));

    [RelayCommand]
    private Task Reload() => RunAsync(() => _config.GetAsync());

    [RelayCommand]
    private async Task ExportLogs()
    {
        var folder = await _pickFolder();
        if (string.IsNullOrWhiteSpace(folder)) return;   // отмена диалога
        if (IsBusy) return;
        IsBusy = true; ClearError(); SetStatus(UiText.None);
        try
        {
            var path = await _logs.ExportAsync(folder);
            SetStatus(UiText.Key(LocKeys.Settings_LogsExportedFormat, path));
        }
        catch (Exception ex) { SetError(ex); }
        finally { IsBusy = false; }
    }

    // Кнопка «Обновить сейчас» в баннере. Запускает `update -y`, по исходу прячет баннер и
    // показывает статус. Вывод CLI при ошибке — как есть (английский, не переводим).
    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (_updater is null || IsBusy) return;
        IsBusy = true; ClearError(); SetStatus(UiText.None);
        try
        {
            var r = await _updater.UpdateAsync();
            switch (r.Outcome)
            {
                case UpdateOutcome.Updated:
                    IsUpdateAvailable = false;
                    SetStatus(UiText.Key(LocKeys.Settings_UpdateDone));
                    break;
                case UpdateOutcome.AlreadyLatest:
                    IsUpdateAvailable = false;
                    SetStatus(UiText.Key(LocKeys.Settings_UpdateUpToDate));
                    break;
                default:
                    SetError(UiText.Of(AppError.Cli(
                        string.IsNullOrWhiteSpace(r.Output) ? "update failed" : r.Output)));
                    break;
            }
        }
        catch (Exception ex) { SetError(ex); }
        finally { IsBusy = false; }
    }

    private async Task RunAsync(Func<Task<VpnConfig>> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        SetStatus(UiText.None);
        try
        {
            Apply(await action());
            SetStatus(UiText.Key(LocKeys.Common_Saved));
        }
        catch (Exception ex) { SetError(ex); }
        finally { IsBusy = false; }
    }

    // Как RunAsync, но действие возвращает текст (create-route-script конфиг не меняет).
    private async Task RunAsyncText(Func<Task<string>> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        SetStatus(UiText.None);
        try
        {
            var text = await action();
            // Вывод create-route-script — текст самого CLI, переводить нечего.
            SetStatus(string.IsNullOrWhiteSpace(text)
                ? UiText.Key(LocKeys.Common_Saved)
                : UiText.Of(AppError.Cli(text)));
        }
        catch (Exception ex) { SetError(ex); }
        finally { IsBusy = false; }
    }

    private void SetStatus(UiText value)
    {
        _statusValue = value;
        StatusText = value.Text;
    }

    private void SetUpdateMessage(UiText value)
    {
        _updateValue = value;
        UpdateMessage = value.Text ?? "";
    }

    /// <summary>Пересобирает статус и плашку обновления после смены языка.</summary>
    private void RefreshTexts()
    {
        StatusText = _statusValue.Text;
        UpdateMessage = _updateValue.Text ?? "";
    }

    private Task Fail(string messageKey)
    {
        SetError(messageKey);
        SetStatus(UiText.None);
        return Task.CompletedTask;
    }

    private static VpnProtocol ParseProtocol(string v) => v switch
    {
        "http2" => VpnProtocol.Http2,
        "quic" => VpnProtocol.Quic,
        _ => VpnProtocol.Auto,
    };

    private static UpdateChannel ParseChannel(string v) => v switch
    {
        "beta" => UpdateChannel.Beta,
        "nightly" => UpdateChannel.Nightly,
        _ => UpdateChannel.Release,
    };

    private static TunnelRoutingMode ParseRouting(string v) => v switch
    {
        "script" => TunnelRoutingMode.Script,
        "none" => TunnelRoutingMode.None,
        _ => TunnelRoutingMode.Auto,
    };
}
