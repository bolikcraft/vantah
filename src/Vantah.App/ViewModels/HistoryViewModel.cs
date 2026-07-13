using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.App.Services;
using Vantah.Core.History;
using Vantah.Core.State;

namespace Vantah.App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly VpnCoordinator _coordinator;

    [ObservableProperty] private bool _hasHistory;
    public ObservableCollection<string> Items { get; } = new();

    public HistoryViewModel(VpnCoordinator coordinator, AppStateStore store)
    {
        _coordinator = coordinator;
        // Любое изменение состояния (подключение/отключение/смена локации) может
        // завершить сессию — перечитываем историю. Маршалим в UI-поток, как в StatusViewModel.
        store.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        Items.Clear();
        foreach (var e in _coordinator.PreviousConnections)
            Items.Add(FormatEntry(e));
        HasHistory = Items.Count > 0;
    }

    private static string FormatEntry(ConnectionHistoryEntry e)
    {
        var location = string.IsNullOrEmpty(e.Country) ? e.City : $"{e.City}, {e.Country}";
        var started = e.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var ended = e.EndedAt is { } x ? x.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "—";
        return $"{location} — {started} → {ended}";
    }
}
