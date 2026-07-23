using Avalonia.Headless.XUnit;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Localization;
using Vantah.Core.State;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

public class ConfigViewKillSwitchTests
{
    [AvaloniaFact]
    public async Task Toggling_kill_switch_persists_to_store()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var killStore = new KillSwitchStore(Path.Combine(dir, "killswitch"));
        var svc = new FakeConfigService();
        var vm = new ConfigViewModel(svc, new AppStateStore(),
            new LanguageStore(Path.Combine(dir, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            killSwitch: killStore);
        await vm.LoadTask;

        vm.KillSwitchEnabled = true;

        Assert.True(killStore.Load());
    }
}
