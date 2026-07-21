using Avalonia.Headless.XUnit;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Autostart;
using Vantah.Core.Localization;
using Vantah.Core.State;
using Vantah.Core.Update;
using Vantah.Core.Vpn;
using Xunit;

/// <summary>Тумблер «Проверять обновления Vantah»: источник истины — AppUpdateStore.</summary>
public class ConfigViewUpdateCheckTests
{
    private sealed class StubSource : IAppReleaseSource
    {
        public Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default) =>
            Task.FromResult<AppUpdateInfo?>(null);
    }

    private static (ConfigViewModel Vm, AppUpdateStore Store) Build()
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var updateStore = new AppUpdateStore(Path.Combine(root, "appupdate.json"));
        var appUpdates = new AppUpdateService(new StubSource(), updateStore, "0.1.0");
        var vm = new ConfigViewModel(
            new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            new AutoConnectStore(Path.Combine(root, "autoconnect")),
            new AutostartService(Path.Combine(root, "autostart"), "vantah", "vantah"),
            appUpdates);
        return (vm, updateStore);
    }

    [AvaloniaFact]
    public void Checked_by_default()
    {
        var (vm, _) = Build();

        Assert.True(vm.CheckAppUpdates);
    }

    [AvaloniaFact]
    public void Loading_the_form_does_not_write_the_store()
    {
        var (_, store) = Build();

        Assert.Null(store.Load().LastCheckUtc);
        Assert.True(store.Load().Enabled);
    }

    [AvaloniaFact]
    public void Unchecking_disables_the_check()
    {
        var (vm, store) = Build();

        vm.CheckAppUpdates = false;

        Assert.False(store.Load().Enabled);
    }

    [AvaloniaFact]
    public void Rechecking_enables_it_again()
    {
        var (vm, store) = Build();

        vm.CheckAppUpdates = false;
        vm.CheckAppUpdates = true;

        Assert.True(store.Load().Enabled);
    }
}
