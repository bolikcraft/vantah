using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.Core.Cli;

namespace Vantah.App.ViewModels;

/// <summary>Вкладка «Процессы»: живые процессы CLI с возможностью убить один или все.</summary>
public partial class ProcessesViewModel : ObservableObject
{
    private readonly IProcessMonitor _monitor;

    [ObservableProperty] private bool _isEmpty = true;

    public ObservableCollection<ProcessRowViewModel> Processes { get; } = new();

    public ProcessesViewModel(IProcessMonitor monitor)
    {
        _monitor = monitor;
        // Changed приходит из произвольного потока (в т.ч. из пула при завершении процесса) —
        // обновление коллекции обязательно маршалим в UI-поток.
        _monitor.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        // Строки уже открытых Flyout сами не перечитываются — после смены языка
        // просим каждую строку обновить свой текст подтверждения.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            foreach (var row in Processes)
                row.RefreshLocalization();
        });
        Refresh();
    }

    private void Refresh()
    {
        Processes.Clear();
        foreach (var p in _monitor.Snapshot())
            Processes.Add(new ProcessRowViewModel(p, KillAsync));
        IsEmpty = Processes.Count == 0;
    }

    private Task KillAsync(long id) => _monitor.KillAsync(id);

    [RelayCommand]
    private Task KillAll() => _monitor.KillAllAsync();
}
