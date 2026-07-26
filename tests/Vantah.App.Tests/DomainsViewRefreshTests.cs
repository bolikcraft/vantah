using Vantah.Core.Cli;
using Vantah.Core.Errors;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Vantah.Core.State;

/// <summary>
/// Сбой первой загрузки исключений (у пользователя — таймаут «adguardvpn-cli site-exclusions show»)
/// оставлял вкладку «Домены» пустой навсегда: ни кнопки повтора, ни перечитывания списка.
/// Проверяем на отрисованном экране, что кнопка «Обновить» есть и действительно перезагружает список.
/// </summary>
public class DomainsViewRefreshTests
{
    // Первый GetAsync падает, последующие отдают снимок — как «CLI подвис, потом ожил».
    private sealed class FlakyExclusions(ExclusionsSnapshot snapshot) : IExclusionsService
    {
        private int _calls;

        public async Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default)
        {
            await Task.Yield();
            if (Interlocked.Increment(ref _calls) == 1)
                throw new CliTimeoutException(new AppError(AppErrorCode.Timeout, "adguardvpn-cli site-exclusions show"));
            return snapshot;
        }

        public Task AddAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;

        public Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
            IReadOnlyList<string> currentDomains, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static DomainsViewModel MakeVm(IExclusionsService exclusions)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new ExclusionsStore(Path.Combine(dir, "site-exclusions"));
        return new DomainsViewModel(exclusions, store, new AppStateStore());
    }

    [AvaloniaFact]
    public async Task Refresh_button_reloads_list_after_failed_load()
    {
        var vm = MakeVm(new FlakyExclusions(new ExclusionsSnapshot(SiteExclusionMode.General, ["example.com"])));
        var window = new Window { Content = new DomainsView { DataContext = vm }, Width = 600, Height = 600 };
        window.Show();

        await vm.LoadTask;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        // Сбой ПЕРВОЙ загрузки описывает состояние всей вкладки, а не отдельного действия:
        // сообщение уходит в LoadError (вместо списка), общий баннер остаётся пустым.
        Assert.NotNull(vm.LoadError);
        Assert.Null(vm.Error);
        Assert.Empty(vm.Items);

        // Кнопок «Обновить» в разметке две (нижний ряд и блок сбоя), но видна ровно одна —
        // та, что относится к текущему состоянию экрана.
        var refresh = window.GetVisualDescendants().OfType<Button>()
            .Single(b => Equals(b.Content, Localizer.Instance[LocKeys.Common_Refresh]) && b.IsEffectivelyVisible);
        Assert.True(refresh.IsEffectivelyEnabled);
        // RaiseEvent(ClickEvent) команду кнопки не запускает (её дёргает Button.OnClick),
        // поэтому нажатие эмулируем вызовом той самой команды, что привязана к кнопке.
        Assert.NotNull(refresh.Command);
        refresh.Command!.Execute(refresh.CommandParameter);

        await vm.LoadTask;

        Assert.Null(vm.Error);
        Assert.Single(vm.Items);
    }

    // Возврат на вкладку «Домены» после сбоя — повторная попытка без ручного нажатия.
    [AvaloniaFact]
    public async Task Reload_if_failed_retries_only_after_failure()
    {
        var flaky = new FlakyExclusions(new ExclusionsSnapshot(SiteExclusionMode.General, ["example.com"]));
        var vm = MakeVm(flaky);

        await vm.LoadTask;
        Assert.NotNull(vm.LoadError);
        Assert.Null(vm.Error);

        vm.ReloadIfFailed();
        await vm.LoadTask;
        Assert.Single(vm.Items);

        // Успешная загрузка — повторное открытие вкладки CLI больше не дёргает.
        var before = vm.LoadTask;
        vm.ReloadIfFailed();
        Assert.Same(before, vm.LoadTask);
    }
}
