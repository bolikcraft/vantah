using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Localization;
using Vantah.Core.State;
using Vantah.Core.Update;
using Xunit;

public class ConfigViewModelUpdateInstallTests
{
    private static ConfigViewModel Vm(FakeCliUpdater updater) =>
        new(new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "language")),
            new FakeUpdateChecker(new UpdateStatus(IsLatest: false, LatestVersion: "1.8.0")),
            new FakeLogExporter(), () => Task.FromResult<string?>(null),
            updater: updater);

    [Fact]
    public async Task Successful_install_hides_banner_and_runs_updater()
    {
        var updater = new FakeCliUpdater(new UpdateResult(UpdateOutcome.Updated, "done"));
        var vm = Vm(updater);
        await vm.UpdateCheckTask;                 // проставит IsUpdateAvailable = true
        Assert.True(vm.IsUpdateAvailable);

        await vm.InstallUpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, updater.Calls);
        Assert.False(vm.IsUpdateAvailable);       // баннер спрятан
    }
}
