using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Localization;
using Vantah.Core.State;
using Xunit;

public class ConfigViewModelDnsPresetTests
{
    private static ConfigViewModel Vm(FakeConfigService svc) =>
        new(svc, new AppStateStore(),
            new LanguageStore(Path.Combine(Path.GetTempPath(), "vantah-tests",
                Guid.NewGuid().ToString("N"), "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null));

    [Fact]
    public async Task Preset_sends_set_dns_with_the_preset_upstream()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        await vm.LoadTask;

        await vm.SelectDnsPresetCommand.ExecuteAsync("1.1.1.1");

        Assert.Contains("set-dns:1.1.1.1", svc.Calls);
        Assert.Equal("1.1.1.1", vm.DnsUpstream);
    }
}
