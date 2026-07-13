using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Services;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.ViewModels;

public partial class StatusViewModel : ObservableObject
{
    private readonly VpnCoordinator _coordinator;
    private readonly VpnLogReader _logReader;

    [ObservableProperty] private string _connectionText = "Отключено";
    [ObservableProperty] private string? _location;
    [ObservableProperty] private string? _mode;
    [ObservableProperty] private string _rxText = "↓ 0.0 B/s";
    [ObservableProperty] private string _txText = "↑ 0.0 B/s";
    [ObservableProperty] private string _rxTotalText = "↓ 0.0 B";
    [ObservableProperty] private string _txTotalText = "↑ 0.0 B";
    [ObservableProperty] private string? _error;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    private bool _isConnected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _log = "";

    // Подпись кнопки = действие, которое она выполнит (не статус).
    public string ToggleText => IsConnected ? "Отключить" : "Подключить";

    public StatusViewModel(VpnCoordinator coordinator, AppStateStore store, VpnLogReader logReader)
    {
        _coordinator = coordinator;
        _logReader = logReader;
        store.Changed += (_, s) => Dispatcher.UIThread.Post(() => Apply(s));
        Apply(store.Current);

        // Периодически подтягиваем хвост VPN-лога в текст-поле.
        RefreshLog();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => RefreshLog();
        timer.Start();
    }

    private void RefreshLog() => Log = string.Join('\n', _logReader.ReadTail());

    private void Apply(AppSnapshot s)
    {
        ConnectionText = s.Connection switch
        {
            ConnectionState.Connected     => "Подключено",
            ConnectionState.Connecting    => "Подключаюсь…",
            ConnectionState.Disconnecting => "Отключаюсь…",
            ConnectionState.Error         => "Ошибка",
            _                             => "Отключено",
        };
        Location = s.Location;
        Mode = s.Mode;
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
