using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Services;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.ViewModels;

public partial class StatusViewModel : ObservableObject
{
    private readonly VpnCoordinator _coordinator;

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

    private ConnectionState? _lastConnection;

    // Подпись кнопки = действие, которое она выполнит (не статус).
    public string ToggleText => IsConnected ? "Отключить" : "Подключить";

    public StatusViewModel(VpnCoordinator coordinator, AppStateStore store)
    {
        _coordinator = coordinator;
        _lastConnection = store.Current.Connection;   // не логируем стартовое состояние
        store.Changed += (_, s) => Dispatcher.UIThread.Post(() => Apply(s));
        Apply(store.Current);
    }

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

        // Событие в лог — только при смене состояния подключения.
        if (_lastConnection != s.Connection)
        {
            _lastConnection = s.Connection;
            AppendLog(StateLogMessage(s));
        }
    }

    [RelayCommand]
    private Task Toggle() =>
        IsConnected ? _coordinator.DisconnectAsync() : _coordinator.ConnectAsync(null, fastest: false);

    private static string StateLogMessage(AppSnapshot s) => s.Connection switch
    {
        ConnectionState.Connecting    => "Подключение…",
        ConnectionState.Connected     => $"Подключено: {s.Location} ({s.Mode})",
        ConnectionState.Disconnecting => "Отключение…",
        ConnectionState.Error         => $"Ошибка: {s.Error}",
        _                             => "Отключено",
    };

    private void AppendLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        Log = string.IsNullOrEmpty(Log) ? line : line + "\n" + Log;   // новые сверху
    }

    private static string Format(double bytesPerSec)
    {
        string[] u = { "B", "KB", "MB", "GB" };
        double v = bytesPerSec; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.0} {u[i]}";
    }
}
