using Avalonia.Headless.XUnit;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Autostart;
using Vantah.Core.Models;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Vantah.Core.Vpn;

/// <summary>
/// SOCKS-аутентификация на вкладке «Настройки»: одна кнопка применяет логин и пароль,
/// поэтому важно, что именно уходит в CLI и что остаётся в форме.
/// </summary>
public class ConfigViewModelSocksTests
{
    private static (ConfigViewModel Vm, FakeConfigService Config) MakeVm()
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        // Свои временные пути: прогон тестов не трогает настоящие ~/.config/vantah/*.
        var config = new FakeConfigService(new VpnConfig());
        var vm = new ConfigViewModel(
            config,
            new AppStateStore(),
            new Vantah.Core.Localization.LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(),
            new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            new AutoConnectStore(Path.Combine(root, "autoconnect")),
            new AutostartService(Path.Combine(root, "autostart"), "vantah", "vantah"));
        return (vm, config);
    }

    // После успешного сохранения пароль SOCKS не должен оставаться в форме — но уйти в CLI
    // он обязан целиком (иначе очистка «до отправки» осталась бы незамеченной).
    [AvaloniaFact]
    public async Task Socks_password_is_sent_and_then_cleared_from_the_form()
    {
        var (vm, config) = MakeVm();
        await vm.LoadTask;
        vm.SocksUsername = "user";
        vm.SocksPassword = "S3cr3t!";

        await vm.ApplySocksAuthCommand.ExecuteAsync(null);

        Assert.Contains("set-socks-username:user", config.Calls);
        Assert.Contains("set-socks-password:S3cr3t!", config.Calls);
        Assert.Equal("", vm.SocksPassword);
        Assert.Null(vm.Error);
    }

    // Регресс: поле пустое после первого применения, поэтому повторное «Применить»
    // (например, чтобы поправить опечатку в логине) не должно стирать пароль в CLI.
    // Сброс аутентификации — отдельная команда ClearSocksAuth.
    [AvaloniaFact]
    public async Task Repeated_apply_with_empty_password_does_not_reset_it()
    {
        var (vm, config) = MakeVm();
        await vm.LoadTask;
        vm.SocksUsername = "user";
        vm.SocksPassword = "S3cr3t!";
        await vm.ApplySocksAuthCommand.ExecuteAsync(null);

        vm.SocksUsername = "corrected";
        await vm.ApplySocksAuthCommand.ExecuteAsync(null);

        Assert.Contains("set-socks-username:corrected", config.Calls);
        Assert.Single(config.Calls, c => c.StartsWith("set-socks-password:"));
        Assert.DoesNotContain("set-socks-password:", config.Calls);
        Assert.Null(vm.Error);
    }
}
