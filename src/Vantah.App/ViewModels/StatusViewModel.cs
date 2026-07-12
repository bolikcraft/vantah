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
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isBusy;

    public StatusViewModel(VpnCoordinator coordinator, AppStateStore store)
    {
        _coordinator = coordinator;
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
        }
        else
        {
            RxText = "↓ 0.0 B/s";
            TxText = "↑ 0.0 B/s";
        }
    }

    [RelayCommand]
    private Task Toggle() =>
        IsConnected ? _coordinator.DisconnectAsync() : _coordinator.ConnectAsync(null, fastest: false);

    [RelayCommand]
    private Task Fastest() => _coordinator.ConnectAsync(null, fastest: true);

    private static string Format(double bytesPerSec)
    {
        string[] u = { "B", "KB", "MB", "GB" };
        double v = bytesPerSec; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.0} {u[i]}";
    }
}
