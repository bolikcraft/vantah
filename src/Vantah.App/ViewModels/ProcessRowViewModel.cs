using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.Core.Cli;

namespace Vantah.App.ViewModels;

/// <summary>Строка таблицы процессов: снимок записи реестра + команда «убить» по внутреннему id.</summary>
public partial class ProcessRowViewModel : ObservableObject
{
    private readonly long _id;
    private readonly Func<long, Task> _kill;

    public ProcessRowViewModel(RunningProcess process, Func<long, Task> kill)
    {
        _id = process.Id;
        _kill = kill;
        Pid = process.Pid;
        CommandLine = process.CommandLine;
        StartedAtText = process.StartedAt.ToLocalTime().ToString("HH:mm:ss");
    }

    public int Pid { get; }
    public string CommandLine { get; }
    public string StartedAtText { get; }

    /// <summary>
    /// Текст подтверждения во всплывашке «Kill». Локализованный формат нельзя подставить
    /// в <c>StringFormat</c> привязки, поэтому текст собирается здесь.
    /// Пересчитывается при смене языка: <see cref="ProcessesViewModel"/> дёргает
    /// <see cref="RefreshLocalization"/> — содержимое Flyout создаётся один раз и само
    /// перечитать строку не может.
    /// </summary>
    public string KillConfirmText => Localizer.Instance.Format(LocKeys.Processes_KillConfirm, Pid);

    /// <summary>Перечитать локализованные строки строки-модели после смены языка.</summary>
    internal void RefreshLocalization() => OnPropertyChanged(nameof(KillConfirmText));

    [RelayCommand]
    private Task Kill() => _kill(_id);
}
