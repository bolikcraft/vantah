using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.History;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

/// <summary>
/// До первого опроса CLI статус неизвестен: вместо «Отключено» должен висеть скелетон,
/// а кнопка Toggle — быть заблокированной.
/// </summary>
public class StatusViewSkeletonTests
{
    [AvaloniaFact]
    public void Fresh_view_model_starts_in_unknown_state()
    {
        var (vm, _) = MakeStatusVm();

        Assert.True(vm.IsStatusUnknown);
        Assert.True(vm.IsBusy);
    }

    [AvaloniaFact]
    public void First_poll_result_clears_the_unknown_state()
    {
        var (vm, store) = MakeStatusVm();

        store.Set(s => s with { Connection = ConnectionState.Disconnected });
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsStatusUnknown);
        Assert.False(vm.IsBusy);
    }

    /// <summary>
    /// Зелёная сборка не доказывает, что скелетон реально виден: биндинг IsVisible рвётся молча.
    /// Гоняем настоящий StatusView в headless-окне и смотрим, какая из двух панелей на экране.
    /// </summary>
    [AvaloniaFact]
    public void The_view_swaps_skeleton_for_the_real_status_block()
    {
        var (vm, store) = MakeStatusVm();
        var view = new StatusView { DataContext = vm };
        var window = new Window { Content = view, Width = 600, Height = 900 };
        window.Show();

        var skeleton = SkeletonPanel(window);
        var real = RealStatusPanel(window);

        Assert.True(skeleton.IsVisible);
        Assert.False(real.IsVisible);

        store.Set(s => s with { Connection = ConnectionState.Disconnected });
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.False(skeleton.IsVisible);
        Assert.True(real.IsVisible);
    }

    private static StackPanel SkeletonPanel(Window w) => Panel(w, "StatusSkeleton");

    private static StackPanel RealStatusPanel(Window w) => Panel(w, "StatusBlock");

    private static StackPanel Panel(Window w, string name) =>
        w.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == name);

    private static (StatusViewModel Vm, AppStateStore Store) MakeStatusVm()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var coord = new VpnCoordinator(new FakeVpnService(), traffic, store, history, ipStore, new FakeAuthService());
        var logReader = new VpnLogReader(Path.Combine(dir, "vpn.log"));
        var vm = new StatusViewModel(coord, store, logReader, new HistoryViewModel(coord, store), ipStore);
        return (vm, store);
    }
}
