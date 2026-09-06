using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Localization;
using Vantah.Core.Notifications;
using Vantah.Core.State;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Тумблер «Сообщать о потере VPN»: включён по умолчанию, пишет в стор и виден в окне
/// настроек с переведённой подписью и справкой.
/// </summary>
public class ConfigViewNotificationsTests
{
    private static (ConfigViewModel Vm, NotificationsStore Store, string StorePath) Vm()
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var storePath = Path.Combine(root, "notifications");
        var store = new NotificationsStore(storePath);
        var vm = new ConfigViewModel(
            new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            notifications: store);
        return (vm, store, storePath);
    }

    private static ToggleSwitch AlertsSwitch(Window window)
    {
        var label = Localizer.Instance[LocKeys.Settings_ConnectionAlerts];
        return window.GetVisualDescendants().OfType<ToggleSwitch>()
            .Single(t => (t.OnContent as TextBlock)?.Text == label
                      && (t.OffContent as TextBlock)?.Text == label);
    }

    [AvaloniaFact]
    public async Task Alerts_are_on_after_a_clean_start_and_load_writes_nothing()
    {
        var (vm, _, storePath) = Vm();
        await vm.LoadTask;

        Assert.True(vm.ConnectionAlerts);
        // Заполнение формы не должно улетать обратно записью: файла настройки быть не должно.
        Assert.False(File.Exists(storePath));
    }

    [AvaloniaFact]
    public async Task Toggling_alerts_persists()
    {
        var (vm, store, _) = Vm();
        await vm.LoadTask;

        vm.ConnectionAlerts = false;
        Assert.False(store.Load());

        vm.ConnectionAlerts = true;
        Assert.True(store.Load());
    }

    [AvaloniaFact]
    public async Task Settings_window_shows_the_switch_with_a_translated_label_and_help()
    {
        var (vm, _, _) = Vm();
        await vm.LoadTask;
        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 800, Height = 900 };
        window.Show();

        var label = Localizer.Instance[LocKeys.Settings_ConnectionAlerts];
        var help = Localizer.Instance[LocKeys.Settings_ConnectionAlertsHelp];
        Assert.NotEqual(LocKeys.Settings_ConnectionAlerts, label);
        Assert.NotEqual(LocKeys.Settings_ConnectionAlertsHelp, help);

        Assert.True(AlertsSwitch(window).IsChecked);

        var hint = window.GetVisualDescendants().OfType<Control>()
            .Select(ToolTip.GetTip).OfType<TextBlock>()
            .Single(t => t.Text == help);
        Assert.NotNull(hint);
    }

    [AvaloniaFact]
    public async Task Toggling_the_rendered_switch_writes_the_setting()
    {
        var (vm, store, _) = Vm();
        await vm.LoadTask;
        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 800, Height = 900 };
        window.Show();

        AlertsSwitch(window).IsChecked = false;

        Assert.False(vm.ConnectionAlerts);
        Assert.False(store.Load());
    }
}
