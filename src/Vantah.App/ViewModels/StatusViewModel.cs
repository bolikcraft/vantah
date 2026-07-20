using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Vpn;

namespace Vantah.App.ViewModels;

public partial class StatusViewModel : ObservableObject
{
    private readonly VpnCoordinator _coordinator;
    private readonly VpnLogReader _logReader;
    private readonly AppStateStore _store;
    private readonly IpVersionStore _ipVersionStore;

    [ObservableProperty] private string _connectionText = Localizer.Instance[LocKeys.Status_Disconnected];
    [ObservableProperty] private string? _location;
    [ObservableProperty] private string? _mode;
    [ObservableProperty] private string _rxText = "↓ 0.0 B/s";
    [ObservableProperty] private string _txText = "↑ 0.0 B/s";
    [ObservableProperty] private string _rxTotalText = "↓ 0.0 B";
    [ObservableProperty] private string _txTotalText = "↑ 0.0 B";
    [ObservableProperty] private string _exclusionsText = "";
    [ObservableProperty] private string? _error;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    private bool _isConnected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _log = "";

    // Подпись кнопки = действие, которое она выполнит (не статус).
    public string ToggleText => Localizer.Instance[IsConnected ? LocKeys.Common_Disconnect : LocKeys.Common_Connect];

    // История подключений — под-панель на вкладке Статус.
    public HistoryViewModel History { get; }

    public IReadOnlyList<IpVersionPreference> IpVersionOptions { get; } =
        [IpVersionPreference.Auto, IpVersionPreference.IPv4Only, IpVersionPreference.IPv6Only];

    [ObservableProperty] private IpVersionPreference _selectedIpVersion;

    partial void OnSelectedIpVersionChanged(IpVersionPreference value) =>
        _ipVersionStore.Save(value);

    public StatusViewModel(
        VpnCoordinator coordinator, AppStateStore store, VpnLogReader logReader,
        HistoryViewModel history, IpVersionStore ipVersionStore)
    {
        _coordinator = coordinator;
        _logReader = logReader;
        _store = store;
        _ipVersionStore = ipVersionStore;
        History = history;
        // В обход сеттера: иначе конструктор тут же перезаписал бы файл только что прочитанным
        // значением (лишняя запись при каждом старте).
        _selectedIpVersion = ipVersionStore.Load();
        store.Changed += (_, s) => Dispatcher.UIThread.Post(() => Apply(s));
        Apply(store.Current);

        // Тексты статуса собираются в коде, поэтому после смены языка их надо пересобрать
        // из последнего снимка состояния — иначе они останутся на прежнем языке до перезапуска.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            Apply(_store.Current);
            OnPropertyChanged(nameof(ToggleText));
        });

        // Периодически подтягиваем хвост VPN-лога в текст-поле.
        RefreshLog();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => RefreshLog();
        timer.Start();
    }

    // Последняя (пере)загрузка хвоста лога — чтобы её можно было дождаться в тестах.
    public Task LogRefreshTask { get; private set; } = Task.CompletedTask;

    private void RefreshLog() => LogRefreshTask = RefreshLogAsync();

    private async Task RefreshLogAsync()
    {
        try
        {
            var tail = await Task.Run(() => _logReader.ReadTail());
            var text = string.Join('\n', tail);
            await Dispatcher.UIThread.InvokeAsync(() => Log = text);
        }
        catch
        {
            // чтение лога не критично
        }
    }

    private void Apply(AppSnapshot s)
    {
        var loc = Localizer.Instance;
        ConnectionText = loc[s.Connection switch
        {
            ConnectionState.Connected     => LocKeys.Status_Connected,
            ConnectionState.Connecting    => LocKeys.Status_Connecting,
            ConnectionState.Disconnecting => LocKeys.Status_Disconnecting,
            ConnectionState.Error         => LocKeys.Status_Error,
            _                             => LocKeys.Status_Disconnected,
        }];
        Location = s.LocationDisplay ?? s.Location;                  // полная локация: «Amsterdam, Netherlands»
        Mode = s.Mode is { } m ? loc.Format(LocKeys.Status_ModeFormat, m) : null;   // «Режим: TUN»
        ExclusionsText = loc[s.ExclusionsMode == SiteExclusionMode.Selective
            ? LocKeys.Status_Exclusions_Selective
            : LocKeys.Status_Exclusions_General];
        Error = s.Error;
        IsConnected = s.Connection == ConnectionState.Connected;
        IsBusy = s.Connection is ConnectionState.Connecting or ConnectionState.Disconnecting;
        if (s.Traffic is { } t)
        {
            RxText = "↓ " + Format(t.RxBytesPerSec) + "/s";
            TxText = "↑ " + Format(t.TxBytesPerSec) + "/s";
            RxTotalText = "↓ " + Format(t.RxBytes);
            TxTotalText = "↑ " + Format(t.TxBytes);
        }
        else
        {
            RxText = "↓ 0.0 B/s";
            TxText = "↑ 0.0 B/s";
            RxTotalText = "↓ 0.0 B";
            TxTotalText = "↑ 0.0 B";
        }
    }

    [RelayCommand]
    private Task Toggle() =>
        IsConnected ? _coordinator.DisconnectAsync() : _coordinator.ConnectAsync(null, fastest: false);

    private static string Format(double bytesPerSec)
    {
        string[] u = { "B", "KB", "MB", "GB" };
        double v = bytesPerSec; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.0} {u[i]}";
    }
}
