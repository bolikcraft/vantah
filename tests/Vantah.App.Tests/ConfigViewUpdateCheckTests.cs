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

    private static (ConfigViewModel Vm, AppUpdateStore Store, string Path) Build(
        AppUpdateState? saved = null)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var statePath = System.IO.Path.Combine(root, "appupdate.json");
        var updateStore = new AppUpdateStore(statePath);
        if (saved is { } s) updateStore.Save(s);
        var appUpdates = new AppUpdateService(new StubSource(), updateStore, "0.1.0");
        var vm = new ConfigViewModel(
            new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            new AutoConnectStore(Path.Combine(root, "autoconnect")),
            new AutostartService(Path.Combine(root, "autostart"), "vantah", "vantah"),
            appUpdates);
        return (vm, updateStore, statePath);
    }

    [AvaloniaFact]
    public void Checked_by_default()
    {
        var (vm, _, _) = Build();

        Assert.True(vm.CheckAppUpdates);
    }

    [AvaloniaFact]
    public void Form_reflects_a_previously_disabled_check()
    {
        // Регрессия, которую пользователь замечает первой: снял галку — после перезапуска
        // она снова стоит.
        var (vm, _, _) = Build(new AppUpdateState { Enabled = false });

        Assert.False(vm.CheckAppUpdates);
    }

    [AvaloniaFact]
    public void Loading_the_form_does_not_write_the_store()
    {
        // Проверяем именно отсутствие файла: Load() на пустом пути вернул бы дефолты
        // независимо от того, писала форма или нет, и такой тест не смог бы упасть.
        var (_, _, path) = Build();

        Assert.False(File.Exists(path));
    }

    [AvaloniaFact]
    public void Unchecking_disables_the_check()
    {
        var (vm, store, _) = Build();

        vm.CheckAppUpdates = false;

        Assert.False(store.Load().Enabled);
    }

    [AvaloniaFact]
    public void Rechecking_enables_it_again()
    {
        var (vm, store, _) = Build();

        vm.CheckAppUpdates = false;
        vm.CheckAppUpdates = true;

        Assert.True(store.Load().Enabled);
    }
}
