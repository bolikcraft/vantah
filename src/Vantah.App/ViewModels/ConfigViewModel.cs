using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.Core.Localization;
using Vantah.Core.Models;
using Vantah.Core.Settings;
using Vantah.Core.State;

namespace Vantah.App.ViewModels;

/// <summary>Язык интерфейса в выпадающем списке. Display — самоназвание, не переводится.</summary>
public sealed record LanguageOption(string Code, string Display);

/// <summary>
/// Вкладка «Настройки». Тумблеры и выпадающие списки применяются сразу, текстовые поля — по кнопке.
/// Форма всегда перерисовывается из ответа CLI, а не из введённого: показываем применённое, а не желаемое.
/// </summary>
public partial class ConfigViewModel : ObservableObject
{
    private readonly IConfigService _config;
    private readonly LanguageStore _languageStore;

    // Подавляет авто-применение, пока форму заполняем программно: иначе загрузка конфига
    // тут же отправила бы обратно в CLI всё, что только что из него прочитали.
    private bool _loading;

    public ConfigViewModel(IConfigService config, AppStateStore store, LanguageStore languageStore)
    {
        _config = config;
        _languageStore = languageStore;

        // Пишем в backing-поле, а не в свойство: стартовый язык — не выбор пользователя,
        // сохранять его в ~/.config/vantah/language незачем.
        var current = Localizer.Instance.Language;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == current) ?? Languages[0];

        store.Changed += (_, s) => Dispatcher.UIThread.Post(() =>
            IsConnectedWarningVisible = s.Connection == ConnectionState.Connected);
        IsConnectedWarningVisible = store.Current.Connection == ConnectionState.Connected;

        _ = LoadAsync();
    }

    /// <summary>Языки интерфейса. Название каждого — на нём самом, поэтому не переводится.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } = new[]
    {
        new LanguageOption("ru", "Русский"),
        new LanguageOption("en", "English"),
    };

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
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private bool _isConnectedWarningVisible;

    private async Task LoadAsync()
    {
        try { Apply(await _config.GetAsync()); }
        catch (Exception ex) { Error = ex.Message; }
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
            await _config.SetSocksUsernameAsync(SocksUsername.Trim());
            return await _config.SetSocksPasswordAsync(SocksPassword);
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

    // Пустое поле — осознанное отключение override; CLI принимает пустой аргумент.
    [RelayCommand]
    private Task ApplyInterfaceOverride() =>
        RunAsync(() => _config.SetBoundIfOverrideAsync(InterfaceOverride.Trim()));

    [RelayCommand]
    private Task Reload() => RunAsync(() => _config.GetAsync());

    private async Task RunAsync(Func<Task<VpnConfig>> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        StatusText = null;
        try
        {
            Apply(await action());
            StatusText = Localizer.Instance[LocKeys.Common_Saved];
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    // Как RunAsync, но действие возвращает текст (create-route-script конфиг не меняет).
    private async Task RunAsyncText(Func<Task<string>> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        StatusText = null;
        try
        {
            var text = await action();
            StatusText = string.IsNullOrWhiteSpace(text)
                ? Localizer.Instance[LocKeys.Common_Saved]
                : text.Trim();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    private Task Fail(string messageKey)
    {
        Error = Localizer.Instance[messageKey];
        StatusText = null;
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
