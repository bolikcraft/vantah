using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Localization;
using Vantah.Core.State;
using Vantah.Core.Update;
using Vantah.App.Views;
using Xunit;

public class ConfigViewUpdateButtonTests
{
    [AvaloniaFact]
    public async Task Update_button_runs_updater_and_hides_banner()
    {
        var updater = new FakeCliUpdater(new UpdateResult(UpdateOutcome.Updated, "done"));
        var vm = new ConfigViewModel(new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "language")),
            new FakeUpdateChecker(new UpdateStatus(false, "1.8.0")),
            new FakeLogExporter(), () => Task.FromResult<string?>(null),
            updater: updater);
        await vm.UpdateCheckTask;

        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 600, Height = 900 };
        window.Show();

        var button = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.GetType() == typeof(Button))
            .First(b => (b.Content as string) == Localizer.Instance[LocKeys.Settings_InstallUpdate]);
        button.Command!.Execute(button.CommandParameter);
        await Task.Delay(50);

        Assert.Equal(1, updater.Calls);
    }
}
