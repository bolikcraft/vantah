using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Vantah.Core.State;

/// <summary>
/// Загрузка вкладки «Домены»: DomainsViewModel.ReloadAsync правит Items и режим (IsGeneral/
/// IsSelective) ПОСЛЕ await IExclusionsService.GetAsync(). В реальном запуске CLI-обёртка
/// возвращает управление на потоке пула, поэтому эту правку маршалим на UI-поток —
/// здесь проверяем наблюдаемый контракт загрузки (данные наполняются без исключения),
/// а не сам факт потокобезопасности: headless-продолжение всегда возвращается на UI-поток.
/// </summary>
public class DomainsViewModelThreadingTests
{
    // GetAsync, отвечающий АСИНХРОННО (через Task.Yield) — как ScriptedVpn в LocationsViewTests.
    private sealed class ScriptedExclusions(ExclusionsSnapshot snapshot) : IExclusionsService
    {
        public async Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default)
        {
            await Task.Yield();
            return snapshot;
        }

        public Task AddAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;

        public Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
            IReadOnlyList<string> currentDomains, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static DomainsViewModel MakeVm(ExclusionsSnapshot snapshot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new ExclusionsStore(Path.Combine(dir, "site-exclusions"));
        return new DomainsViewModel(new ScriptedExclusions(snapshot), store, new AppStateStore());
    }

    [AvaloniaFact]
    public async Task Successful_load_populates_items_and_mode()
    {
        var snapshot = new ExclusionsSnapshot(SiteExclusionMode.Selective, ["example.com", "test.org"]);
        var vm = MakeVm(snapshot);

        await vm.LoadTask;

        Assert.Equal(2, vm.Items.Count);
        Assert.True(vm.IsSelective);
        Assert.False(vm.IsGeneral);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.Error);
    }

    // Заполнение списка, привязанного к отрисованному ListBox, при асинхронном ответе CLI
    // не должно падать и обязано наполнить список — это и есть регресс cross-thread правки.
    [AvaloniaFact]
    public async Task Rendered_list_populates_on_async_load()
    {
        var snapshot = new ExclusionsSnapshot(SiteExclusionMode.General, ["example.com"]);
        var vm = MakeVm(snapshot);
        var window = new Window { Content = new DomainsView { DataContext = vm }, Width = 600, Height = 600 };
        window.Show();

        await vm.LoadTask;

        Assert.Null(vm.Error);
        Assert.Single(vm.Items);
    }
}
