using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.App.Localization;
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
        // Строки истории собираются в коде — после смены языка пересобираем их.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        // store.Changed летит каждые ~4с (меняется traffic-сэмпл), но история при этом
        // обычно та же — не рвём коллекцию зря (иначе сбрасывается позиция ScrollViewer).
        var lines = _coordinator.PreviousConnections.Select(FormatEntry).ToList();
        if (lines.Count == Items.Count && lines.SequenceEqual(Items))
            return;
        Items.Clear();
        foreach (var l in lines) Items.Add(l);
        HasHistory = Items.Count > 0;
    }

    // internal (не private): прямой юнит-тест форматирования строки истории.
    internal static string FormatEntry(ConnectionHistoryEntry e)
    {
        var location = string.IsNullOrEmpty(e.Country) ? e.City : $"{e.City}, {e.Country}";
        var started = e.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var ended = e.EndedAt is { } x ? x.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "—";
        var line = Localizer.Instance.Format(LocKeys.Status_HistoryEntryFormat, location, started, ended);
        // Длительность — только у завершённой сессии. Шаблоны берём сырыми через индексатор
        // (в них живут {0}/{1}), их подставляет SessionDuration, а не Localizer.Format.
        if (e.EndedAt is { } end)
        {
            var dur = SessionDuration.Format(
                end - e.StartedAt,
                Localizer.Instance[LocKeys.Status_DurationHm],
                Localizer.Instance[LocKeys.Status_DurationMinutes]);
            line += $" ({dur})";
        }
        return line;
    }
}
