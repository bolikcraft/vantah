using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Localization;
using Vantah.Core.State;
using Xunit;

public class ConfigViewDnsPresetViewTests
{
    private static ConfigViewModel Vm(FakeConfigService svc) =>
        new(svc, new AppStateStore(),
            new LanguageStore(Path.Combine(Path.GetTempPath(), "vantah-tests",
                Guid.NewGuid().ToString("N"), "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null));

    [AvaloniaFact]
    public async Task Clicking_Cloudflare_preset_applies_its_upstream()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        await vm.LoadTask;

        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 600, Height = 900 };
        window.Show();

        var button = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.GetType() == typeof(Button))
            .First(b => (b.Content as string) == "Cloudflare");
        button.Command!.Execute(button.CommandParameter);
        await vm.LoadTask; // на всякий, команда быстрая; основная проверка ниже

        Assert.Contains("set-dns:1.1.1.1", svc.Calls);
    }
}
