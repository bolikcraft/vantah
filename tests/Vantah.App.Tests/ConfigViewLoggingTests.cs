using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
using Vantah.Core.State;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Тумблер «Писать лог»: пишет ли он в стор, переключает ли сам лог и виден ли он в окне
/// настроек с переведённой подписью.
/// </summary>
public class ConfigViewLoggingTests
{
    private static (ConfigViewModel Vm, LoggingStore Store, string StorePath, FakeAppLog Log) Vm()
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var storePath = Path.Combine(root, "logging");
        var store = new LoggingStore(storePath);
        // Лог с выключенного состояния — таким его создаёт App при настройке по умолчанию.
        var log = new FakeAppLog { Enabled = false };
        var vm = new ConfigViewModel(
            new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            logging: store, appLog: log);
        return (vm, store, storePath, log);
    }

    [AvaloniaFact]
    public async Task Logging_is_off_after_start_and_load_writes_nothing()
    {
        var (vm, _, storePath, log) = Vm();
        await vm.LoadTask;

        Assert.False(vm.LoggingEnabled);
        Assert.False(log.Enabled);
        // Заполнение формы не должно улетать обратно записью: файла настройки быть не должно.
        Assert.False(File.Exists(storePath));
    }

    [AvaloniaFact]
    public async Task Toggling_logging_persists_and_switches_the_log_at_once()
    {
        var (vm, store, _, log) = Vm();
        await vm.LoadTask;

        vm.LoggingEnabled = true;

        Assert.True(store.Load());
        Assert.True(log.Enabled);
        var afterEnable = log.Lines.Count;
        Assert.True(afterEnable > 0);   // включение видно в самом логе

        vm.LoggingEnabled = false;

        Assert.False(store.Load());
        Assert.False(log.Enabled);
        // Строка про выключение написана, пока лог ещё принимал записи.
        Assert.True(log.Lines.Count > afterEnable);
    }

    [AvaloniaFact]
    public async Task Settings_window_shows_the_logging_switch_with_a_translated_label()
    {
        var (vm, _, _, _) = Vm();
        await vm.LoadTask;
        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 800, Height = 900 };
        window.Show();

        var label = Localizer.Instance[LocKeys.Settings_Logging];
        var help = Localizer.Instance[LocKeys.Settings_LoggingHelp];
        Assert.NotEqual(LocKeys.Settings_Logging, label);
        Assert.NotEqual(LocKeys.Settings_LoggingHelp, help);

        var toggle = window.GetVisualDescendants().OfType<ToggleSwitch>()
            .Single(t => (t.OnContent as TextBlock)?.Text == label
                      && (t.OffContent as TextBlock)?.Text == label);
        Assert.False(toggle.IsChecked);

        // Справка живёт в подсказке «?» рядом с тумблером (как у kill switch) и называет путь
        // к файлу — иначе лог негде искать.
        var hint = window.GetVisualDescendants().OfType<Control>()
            .Select(ToolTip.GetTip).OfType<TextBlock>()
            .Single(t => t.Text == help);
        Assert.Contains("~/.local/share/vantah/app.log", hint.Text);
    }

    [AvaloniaFact]
    public async Task Toggling_the_rendered_switch_turns_the_log_on()
    {
        var (vm, store, _, log) = Vm();
        await vm.LoadTask;
        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 800, Height = 900 };
        window.Show();

        var toggle = window.GetVisualDescendants().OfType<ToggleSwitch>()
            .Single(t => (t.OnContent as TextBlock)?.Text == Localizer.Instance[LocKeys.Settings_Logging]
                      && (t.OffContent as TextBlock)?.Text == Localizer.Instance[LocKeys.Settings_Logging]);

        toggle.IsChecked = true;

        Assert.True(vm.LoggingEnabled);
        Assert.True(store.Load());
        Assert.True(log.Enabled);
    }
}
