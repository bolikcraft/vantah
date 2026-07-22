using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Cli;
using Vantah.Core.Errors;
using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.Tests.Localization;

/// <summary>
/// Сообщение об ошибке висело на экране на том языке, который был в момент сбоя: переключение
/// языка его не трогало (текст ошибки хранился готовой строкой, да ещё и приходил из ядра
/// по-русски). Проверяем на отрисованной вкладке «Домены», что текст пересобирается.
/// </summary>
public class ErrorLocalizationTests
{
    private sealed class FailingExclusions : IExclusionsService
    {
        public Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default) =>
            throw new CliTimeoutException(new AppError(AppErrorCode.Timeout, "adguardvpn-cli site-exclusions show"));

        public Task AddAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string domain, CancellationToken ct = default) => Task.CompletedTask;

        public Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
            IReadOnlyList<string> currentDomains, CancellationToken ct = default) => Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task Cli_error_on_screen_follows_language_switch()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var vm = new DomainsViewModel(
            new FailingExclusions(), new ExclusionsStore(Path.Combine(dir, "site-exclusions")), new AppStateStore());
        var window = new Window { Content = new DomainsView { DataContext = vm }, Width = 600, Height = 600 };
        window.Show();

        try
        {
            await vm.LoadTask;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("adguardvpn-cli site-exclusions show превысил таймаут", vm.Error);
            Assert.Equal(vm.Error, ErrorTextOnScreen(window));

            Localizer.Instance.SetLanguage("en");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("adguardvpn-cli site-exclusions show timed out", vm.Error);
            Assert.Equal(vm.Error, ErrorTextOnScreen(window));
        }
        finally
        {
            // Localizer.Instance — синглтон на весь тест-хост: не вернём «ru» — протечёт в соседей.
            Localizer.Instance.SetLanguage("ru");
        }
    }

    private static string? ErrorTextOnScreen(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault(t => t is not null && t.Contains("site-exclusions"));
}
