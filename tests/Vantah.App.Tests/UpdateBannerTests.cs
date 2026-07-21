using Avalonia.Headless.XUnit;
using Vantah.App.ViewModels;
using Vantah.Core.Update;
using Xunit;

public class UpdateBannerTests
{
    private static readonly AppUpdateInfo Info =
        new("v0.2.0", "https://github.com/bolikcraft/vantah/releases/tag/v0.2.0");

    private static (UpdateBannerViewModel Vm, AppUpdateStore Store) Build()
    {
        var path = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "appupdate.json");
        var store = new AppUpdateStore(path);
        var service = new AppUpdateService(new StubSource(), store, "0.1.0");
        return (new UpdateBannerViewModel(service), store);
    }

    private sealed class StubSource : IAppReleaseSource
    {
        public Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default) =>
            Task.FromResult<AppUpdateInfo?>(null);
    }

    [AvaloniaFact]
    public void Hidden_until_an_update_is_shown()
    {
        var (vm, _) = Build();

        Assert.False(vm.IsVisible);
    }

    [AvaloniaFact]
    public void Showing_an_update_fills_the_text_with_the_version()
    {
        var (vm, _) = Build();

        vm.Show(Info);

        Assert.True(vm.IsVisible);
        Assert.Contains("0.2.0", vm.Text);
    }

    [AvaloniaFact]
    public void Dismiss_hides_the_banner_and_remembers_the_version()
    {
        var (vm, store) = Build();
        vm.Show(Info);

        vm.DismissCommand.Execute(null);

        Assert.False(vm.IsVisible);
        Assert.Equal("v0.2.0", store.Load().DismissedVersion);
    }

    [AvaloniaFact]
    public void Open_release_hands_the_url_to_the_browser_opener()
    {
        var (vm, _) = Build();
        string? opened = null;
        vm.BrowserOpener = url => { opened = url; return Task.CompletedTask; };
        vm.Show(Info);

        vm.OpenReleaseCommand.Execute(null);

        Assert.Equal("https://github.com/bolikcraft/vantah/releases/tag/v0.2.0", opened);
    }
}
