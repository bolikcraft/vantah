using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.Tests.Localization;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Cli;
using Vantah.Core.History;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Экран «Статус» без adguardvpn-cli. Каталог AppImageHub снимает окно и распознаёт текст:
/// раньше там было «Error» и «No such file or directory», и тест каталога падал.
/// </summary>
public class StatusViewCliMissingTests
{
    [AvaloniaFact]
    public async Task Failed_poll_without_cli_shows_its_own_title()
    {
        var (vm, coord, _) = Make();

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsCliMissing);
        Assert.False(vm.IsStatusUnknown);
        Assert.Equal("AdGuard VPN CLI needed", vm.ConnectionText);
        // Причину уже называют заголовок и подсказка — красной строки под ними нет.
        Assert.Null(vm.Error);
    }

    [AvaloniaFact]
    public async Task Screen_hides_connect_and_offers_install_link()
    {
        var (vm, coord, _) = Make();
        var window = Show(vm);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Render(window);

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(),
            b => b is not HyperlinkButton && b.IsEffectivelyVisible && Equals(b.Content, vm.ToggleText));
        var link = window.GetVisualDescendants().OfType<HyperlinkButton>().Single(b => b.IsEffectivelyVisible);
        Assert.Equal("How to install", link.Content);
        Assert.Equal(new Uri("https://github.com/AdguardTeam/AdGuardVPNCLI"), link.NavigateUri);
        Assert.Contains(VisibleTexts(window), t => t.StartsWith("Vantah works on top of AdGuard VPN CLI."));
    }

    /// <summary>Всё, что видно на экране в этом состоянии, — без слов, которые бракует каталог.</summary>
    [AvaloniaFact]
    public async Task Nothing_on_screen_trips_the_catalog_ocr_check()
    {
        var (vm, coord, _) = Make();
        var window = Show(vm);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Render(window);

        var texts = VisibleTexts(window);
        Assert.Contains(vm.ConnectionText, texts);   // иначе проверяли бы не тот экран
        Assert.DoesNotContain(texts, AppImageHubOcr.Flags);
    }

    [AvaloniaFact]
    public async Task Title_follows_language_switch()
    {
        var (vm, coord, _) = Make();
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        try
        {
            Localizer.Instance.SetLanguage("ru");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Нужен AdGuard VPN CLI", vm.ConnectionText);
        }
        finally
        {
            // Localizer.Instance — синглтон на весь тест-хост: не вернём язык — протечёт в соседей.
            Localizer.Instance.SetLanguage(CultureSelector.Default);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task Connect_button_comes_back_once_cli_is_installed()
    {
        var (vm, coord, cliPath) = Make();
        var window = Show(vm);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Render(window);
        Assert.True(vm.IsCliMissing);

        File.CreateSymbolicLink(cliPath, new[] { "/bin/true", "/usr/bin/true" }.First(File.Exists));
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Render(window);

        Assert.False(vm.IsCliMissing);
        Assert.Equal(Localizer.Instance[LocKeys.Status_Disconnected], vm.ConnectionText);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(),
            b => b.IsEffectivelyVisible && Equals(b.Content, Localizer.Instance[LocKeys.Common_Connect]));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<HyperlinkButton>(), b => b.IsEffectivelyVisible);
    }

    private static Window Show(StatusViewModel vm)
    {
        var window = new Window { Content = new StatusView { DataContext = vm }, Width = 800, Height = 800 };
        window.Show();
        return window;
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static List<string> VisibleTexts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text))
            .Select(t => t.Text!)
            .ToList();

    private static (StatusViewModel Vm, VpnCoordinator Coordinator, string CliPath) Make()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cliPath = Path.Combine(dir, "adguardvpn-cli");

        var store = new AppStateStore();
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var coord = new VpnCoordinator(
            new VpnService(new CliRunner(cliPath)), new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
                new ActiveSessionStore(Path.Combine(dir, "connection-active"))),
            ipStore, new FakeAuthService());
        var vm = new StatusViewModel(coord, store, new VpnLogReader(Path.Combine(dir, "vpn.log")),
            new HistoryViewModel(coord, store), ipStore);
        return (vm, coord, cliPath);
    }
}
