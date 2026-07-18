using Avalonia.Headless.XUnit;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Autostart;
using Vantah.Core.Localization;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Vpn;
using Xunit;

/// <summary>
/// Автоподключение и автозапуск на вкладке «Настройки»: единый источник истины —
/// AutoConnectStore (чекбокс + радиокнопка «сжимаются» в один AutoConnectMode) и
/// AutostartService (наличие .desktop-файла).
/// </summary>
public class ConfigViewStartupTests
{
    // Свои временные пути на каждый тест: прогон не должен трогать настоящие
    // ~/.config/vantah/autoconnect и ~/.config/autostart/vantah.desktop.
    private static (ConfigViewModel Vm, AutoConnectStore AutoConnect, AutostartService Autostart) Build()
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var autoConnect = new AutoConnectStore(Path.Combine(root, "autoconnect"));
        var autostart = new AutostartService(Path.Combine(root, "autostart"), "vantah", "vantah");
        var vm = new ConfigViewModel(
            new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            autoConnect, autostart);
        return (vm, autoConnect, autostart);
    }

    [AvaloniaFact]
    public void Loading_the_form_does_not_write_the_autoconnect_store()
    {
        var (vm, autoConnect, _) = Build();

        Assert.False(vm.AutoConnectEnabled);
        Assert.Equal(AutoConnectMode.Off, autoConnect.Load());
    }

    [AvaloniaFact]
    public void Enabling_autoconnect_defaults_to_last_used()
    {
        var (vm, autoConnect, _) = Build();

        vm.AutoConnectEnabled = true;

        Assert.Equal(AutoConnectMode.LastUsed, autoConnect.Load());
    }

    [AvaloniaFact]
    public void Enabling_then_choosing_fastest_saves_fastest()
    {
        var (vm, autoConnect, _) = Build();

        vm.AutoConnectEnabled = true;
        vm.AutoConnectUseFastest = true;

        Assert.Equal(AutoConnectMode.Fastest, autoConnect.Load());
    }

    [AvaloniaFact]
    public void Enabling_then_disabling_saves_off()
    {
        var (vm, autoConnect, _) = Build();

        vm.AutoConnectEnabled = true;
        vm.AutoConnectUseFastest = true;
        vm.AutoConnectEnabled = false;

        Assert.Equal(AutoConnectMode.Off, autoConnect.Load());
    }

    [AvaloniaFact]
    public void Unchecking_then_rechecking_restores_the_previous_radio_choice()
    {
        var (vm, autoConnect, _) = Build();

        vm.AutoConnectEnabled = true;
        vm.AutoConnectUseFastest = true;
        vm.AutoConnectEnabled = false;
        vm.AutoConnectEnabled = true;

        Assert.Equal(AutoConnectMode.Fastest, autoConnect.Load());
    }

    [AvaloniaFact]
    public void Toggling_autostart_on_enables_it()
    {
        var (vm, _, autostart) = Build();

        vm.AutostartEnabled = true;

        Assert.True(autostart.IsEnabled());
    }

    [AvaloniaFact]
    public void Toggling_autostart_off_disables_it()
    {
        var (vm, _, autostart) = Build();

        vm.AutostartEnabled = true;
        vm.AutostartEnabled = false;

        Assert.False(autostart.IsEnabled());
    }

    [AvaloniaFact]
    public void Form_reflects_a_previously_saved_autoconnect_mode()
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var autoConnect = new AutoConnectStore(Path.Combine(root, "autoconnect"));
        autoConnect.Save(AutoConnectMode.Fastest);
        var autostart = new AutostartService(Path.Combine(root, "autostart"), "vantah", "vantah");
        autostart.Enable();

        var vm = new ConfigViewModel(
            new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            autoConnect, autostart);

        Assert.True(vm.AutoConnectEnabled);
        Assert.True(vm.AutoConnectUseFastest);
        Assert.True(vm.AutostartEnabled);
    }
}
