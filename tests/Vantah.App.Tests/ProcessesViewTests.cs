using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Cli;
using Xunit;

/// <summary>
/// Вкладка «Процессы» рендерится headless: зелёная сборка не доказывает, что строки видны.
/// Раньше она показывала лишь собственных детей Vantah — мгновенные «status», — и мигала пустотой.
/// </summary>
public class ProcessesViewTests
{
    private const string Exe = "/opt/adguardvpn_cli/adguardvpn-cli";

    /// <summary>Реальный расклад: туннель под двумя обёртками sudo — ровно то, что даёт «sudo -b» у CLI.</summary>
    private static RunningProcess[] LiveTunnel() =>
    [
        new(1086935, 1086935, "sudo", ["-b", "env", Exe, "connect"], DateTimeOffset.Now.AddMinutes(-6)),
        new(1086936, 1086936, "sudo", ["-b", "env", Exe, "connect"], DateTimeOffset.Now.AddMinutes(-6)),
        new(1086937, 1086937, Exe, ["connect", "--no-fork", "-l", "Amsterdam"], DateTimeOffset.Now.AddMinutes(-6)),
    ];

    private static Window Show(ProcessesViewModel vm)
    {
        var window = new Window { Content = new ProcessesView { DataContext = vm }, Width = 800, Height = 400 };
        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void Shows_a_row_per_live_cli_process()
    {
        var vm = new ProcessesViewModel(new StubMonitor(LiveTunnel()));

        var window = Show(vm);

        var rows = window.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
        Assert.Equal(3, rows.Length);
        Assert.False(vm.IsEmpty);
    }

    [AvaloniaFact]
    public void Shows_the_tunnel_command_line_and_its_pid()
    {
        var vm = new ProcessesViewModel(new StubMonitor(LiveTunnel()));

        var window = Show(vm);

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
        Assert.Contains("1086937", texts);
        Assert.Contains(texts, t => t is not null && t.Contains("connect --no-fork -l Amsterdam"));
    }

    [AvaloniaFact]
    public async Task Kill_button_of_a_row_kills_that_row_process()
    {
        var monitor = new StubMonitor(LiveTunnel());
        var vm = new ProcessesViewModel(monitor);
        Show(vm);

        // Команда строки — то, что дёргает кнопка подтверждения во Flyout.
        var tunnelRow = vm.Processes.Single(p => p.Pid == 1086937);
        await tunnelRow.KillCommand.ExecuteAsync(null);

        Assert.Equal([1086937L], monitor.Killed);
    }

    [AvaloniaFact]
    public void Empty_list_shows_the_placeholder_instead_of_rows()
    {
        var vm = new ProcessesViewModel(new StubMonitor());

        var window = Show(vm);

        Assert.Empty(window.GetVisualDescendants().OfType<ListBoxItem>());
        Assert.True(vm.IsEmpty);
    }
}
