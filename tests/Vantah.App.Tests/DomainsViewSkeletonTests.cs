using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Errors;
using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Vpn;

namespace Vantah.App.Tests;

/// <summary>
/// Вкладка «Домены» до ответа `site-exclusions show` не имеет права показывать ни список, ни
/// кнопки: пустой список с рабочей «Очистить» читается как «исключений нет». Проверяем три
/// состояния на настоящем DomainsView в headless-окне — зелёная сборка про биндинги
/// IsVisible ничего не доказывает.
/// </summary>
public class DomainsViewSkeletonTests
{
    /// <summary>Исключения-обманка: чтение можно задержать (Hold) и уронить (GetError).</summary>
    private sealed class GatedExclusions(ExclusionsSnapshot snapshot) : IExclusionsService
    {
        private TaskCompletionSource? _gate;

        public int GetCalls { get; private set; }
        public Exception? GetError { get; set; }

        public void Hold() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release()
        {
            var gate = _gate;
            _gate = null;
            gate?.TrySetResult();
        }

        public async Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default)
        {
            if (_gate is { } gate) await gate.Task;
            GetCalls++;
            if (GetError is not null) throw GetError;
            return snapshot;
        }

        public Task AddAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;

        public Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
            IReadOnlyList<string> currentDomains, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static DomainsViewModel Vm(IExclusionsService exclusions)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        return new DomainsViewModel(exclusions, new ExclusionsStore(Path.Combine(dir, "site-exclusions")),
            new AppStateStore());
    }

    private static Window Show(DomainsViewModel vm)
    {
        var window = new Window { Content = new DomainsView { DataContext = vm }, Width = 700, Height = 600 };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Control Named(Window w, string name) =>
        w.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);

    private static int SkeletonTiles(Window w) =>
        w.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("skeleton"));

    // Настоящие (а не нарисованные заглушками) органы управления вкладки.
    private static bool AnyVisibleControl(Window w) =>
        w.GetVisualDescendants().OfType<Control>()
            .Any(c => c is Button or TextBox or RadioButton && c.IsEffectivelyVisible);

    private static void Settle(Window w)
    {
        Dispatcher.UIThread.RunJobs();
        w.UpdateLayout();
    }

    [AvaloniaFact]
    public async Task Skeleton_stands_in_for_the_content_until_the_list_arrives()
    {
        var svc = new GatedExclusions(new ExclusionsSnapshot(SiteExclusionMode.General, ["example.com"]));
        svc.Hold();
        var vm = Vm(svc);
        var window = Show(vm);

        Assert.True(vm.IsLoading);
        Assert.False(vm.IsLoaded);
        Assert.False(Named(window, "DomainsContent").IsEffectivelyVisible);
        Assert.False(AnyVisibleControl(window));          // ни кнопок, ни поля ввода, ни радио
        Assert.True(Named(window, "DomainsSkeleton").IsEffectivelyVisible);
        Assert.True(SkeletonTiles(window) > 0);

        svc.Release();
        await vm.LoadTask;
        Settle(window);

        Assert.True(vm.IsLoaded);
        Assert.False(Named(window, "DomainsSkeleton").IsEffectivelyVisible);
        Assert.True(Named(window, "DomainsContent").IsEffectivelyVisible);
        Assert.True(AnyVisibleControl(window));
        Assert.Single(vm.Items);
    }

    [AvaloniaFact]
    public async Task Failed_load_shows_the_error_and_refresh_recovers_the_content()
    {
        var svc = new GatedExclusions(new ExclusionsSnapshot(SiteExclusionMode.General, ["example.com"]))
        {
            GetError = new VpnCommandException(AppError.Cli("нет ответа от CLI")),
        };
        var vm = Vm(svc);
        await vm.LoadTask;
        var window = Show(vm);

        Assert.Equal("нет ответа от CLI", vm.LoadError);
        Assert.Null(vm.Error);                            // сбой загрузки — не ошибка действия
        Assert.False(vm.IsLoaded);
        Assert.False(Named(window, "DomainsContent").IsEffectivelyVisible);
        Assert.False(Named(window, "DomainsSkeleton").IsEffectivelyVisible);

        var block = Named(window, "DomainsLoadError");
        Assert.True(block.IsEffectivelyVisible);
        Assert.Contains(block.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text == "нет ответа от CLI" && t.IsEffectivelyVisible);

        // Жмём именно кнопку разметки, а не команду вьюмодели: проверяем проводку.
        var refresh = block.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible);
        svc.GetError = null;
        refresh.Command!.Execute(refresh.CommandParameter);
        await vm.LoadTask;
        Settle(window);

        Assert.Null(vm.LoadError);
        Assert.True(vm.IsLoaded);
        Assert.False(Named(window, "DomainsLoadError").IsEffectivelyVisible);
        Assert.True(Named(window, "DomainsContent").IsEffectivelyVisible);
        Assert.Single(vm.Items);
    }

    // Главное требование: ReloadAsync дёргается после каждого действия со списком, и уже
    // показанный список не должен схлопываться в заглушки — иначе экран мигает на каждое
    // добавление/удаление.
    [AvaloniaFact]
    public async Task Reload_after_success_keeps_the_list_instead_of_the_skeleton()
    {
        var svc = new GatedExclusions(new ExclusionsSnapshot(SiteExclusionMode.General, ["example.com"]));
        var vm = Vm(svc);
        await vm.LoadTask;
        var window = Show(vm);
        Assert.True(Named(window, "DomainsContent").IsEffectivelyVisible);

        // Перезагрузка держится «в воздухе» — самый момент, когда скелетон мог бы мигнуть.
        svc.Hold();
        var reload = vm.ReloadCommand.ExecuteAsync(null);
        Settle(window);

        Assert.False(vm.IsLoading);
        Assert.True(vm.IsLoaded);
        Assert.True(Named(window, "DomainsContent").IsEffectivelyVisible);
        Assert.False(Named(window, "DomainsSkeleton").IsEffectivelyVisible);
        Assert.Single(vm.Items);

        svc.Release();
        await reload;
        await vm.LoadTask;
        Settle(window);

        Assert.False(vm.IsLoading);
        Assert.True(Named(window, "DomainsContent").IsEffectivelyVisible);
        Assert.Equal(2, svc.GetCalls);
    }
}
