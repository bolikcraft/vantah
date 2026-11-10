using Avalonia.Headless.XUnit;
using Vantah.App.ViewModels;
using Vantah.Core.Update;
using Xunit;

public class CliUpdateBannerTests
{
    private sealed class StubUpdater(UpdateResult result) : ICliUpdater
    {
        public Task<UpdateResult> UpdateAsync(CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private static (CliUpdateBannerViewModel Vm, CliUpdateStore Store) Build(
        UpdateResult? result = null)
    {
        var path = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "cliupdate.json");
        var store = new CliUpdateStore(path);
        return (new CliUpdateBannerViewModel(
            new StubUpdater(result ?? new UpdateResult(UpdateOutcome.Updated, "")), store), store);
    }

    [AvaloniaFact]
    public void Hidden_until_shown()
    {
        var (vm, _) = Build();

        Assert.False(vm.IsVisible);
    }

    [AvaloniaFact]
    public void Showing_a_version_fills_the_text()
    {
        var (vm, _) = Build();

        vm.Show("2.3.1");

        Assert.True(vm.IsVisible);
        Assert.Contains("2.3.1", vm.Text);
    }

    [AvaloniaFact]
    public void Showing_without_a_version_uses_the_plain_text()
    {
        var (vm, _) = Build();

        vm.Show(null);

        Assert.True(vm.IsVisible);
        // Локаль теста — английская: строка без плейсхолдера.
        Assert.DoesNotContain("{0}", vm.Text);
        Assert.Contains("AdGuard VPN", vm.Text);
    }

    [AvaloniaFact]
    public void Dismiss_hides_the_banner_and_persists_the_version()
    {
        var (vm, store) = Build();
        vm.Show("2.3.1");

        vm.DismissCommand.Execute(null);

        Assert.False(vm.IsVisible);
        Assert.Equal("2.3.1", store.Load().DismissedVersion);
    }

    [AvaloniaFact]
    public void Dismiss_without_a_version_hides_only()
    {
        var (vm, store) = Build();
        vm.Show(null);

        vm.DismissCommand.Execute(null);

        Assert.False(vm.IsVisible);
        Assert.Null(store.Load().DismissedVersion);
    }

    [AvaloniaFact]
    public void Update_success_marks_the_banner_done_and_hides_the_button()
    {
        var (vm, _) = Build(new UpdateResult(UpdateOutcome.Updated, ""));
        vm.Show("2.3.1");

        vm.UpdateNowCommand.Execute(null);

        Assert.True(vm.IsUpdated);
        Assert.False(vm.IsUpdating);
        // «Vantah» есть в тексте Settings_UpdateDone обоих языков (en: "Updated. Restart Vantah
        // to apply.", ru: «Обновлено. Перезапустите Vantah…») — привязка к локали тест-хоста не нужна.
        Assert.Contains("Vantah", vm.Text);
    }

    [AvaloniaFact]
    public void Already_latest_also_marks_done()
    {
        var (vm, _) = Build(new UpdateResult(UpdateOutcome.AlreadyLatest, ""));
        vm.Show("2.3.1");

        vm.UpdateNowCommand.Execute(null);

        Assert.True(vm.IsUpdated);
    }

    [AvaloniaFact]
    public void Update_failure_keeps_the_button_and_shows_cli_output()
    {
        var (vm, _) = Build(new UpdateResult(UpdateOutcome.Failed, "update failed: network"));
        vm.Show("2.3.1");

        vm.UpdateNowCommand.Execute(null);

        Assert.False(vm.IsUpdated);
        Assert.Equal("update failed: network", vm.Text);
    }

    [AvaloniaFact]
    public void Reshowing_resets_the_done_state()
    {
        var (vm, _) = Build();
        vm.Show("2.3.1");
        vm.UpdateNowCommand.Execute(null);

        vm.Show("2.4.0");

        Assert.False(vm.IsUpdated);
        Assert.Contains("2.4.0", vm.Text);
    }
}
