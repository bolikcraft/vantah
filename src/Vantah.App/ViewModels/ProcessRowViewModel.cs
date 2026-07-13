using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [RelayCommand]
    private Task Kill() => _kill(_id);
}
