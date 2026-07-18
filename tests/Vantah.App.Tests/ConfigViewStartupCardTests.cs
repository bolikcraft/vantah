using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Autostart;
using Vantah.Core.Localization;
using Vantah.Core.Models;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Карточка «Запуск» в настройках рендерится headless. Зелёная сборка не доказывает, что
/// биндинги живые: IsEnabled на под-панели с радиокнопками и двусторонняя связь с
/// AutoConnectStore/AutostartService могут молча отвалиться, а юнит-тест вьюмодели этого не поймает.
/// </summary>
public class ConfigViewStartupCardTests
{
    private static Window Show(ConfigViewModel vm)
    {
        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 600, Height = 900 };
        window.Show();
        return window;
    }

    // AutoConnectStore/AutostartService по умолчанию пишут в ~/.config/vantah — уводим на temp,
    // иначе прогон тестов затронул бы настоящие настройки автозапуска пользователя.
    private static (ConfigViewModel vm, AutoConnectStore autoConnect, AutostartService autostart) VmWithStartup()
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var autoConnect = new AutoConnectStore(Path.Combine(root, "autoconnect"));
        var autostart = new AutostartService(Path.Combine(root, "autostart"), "vantah", "vantah");
        var vm = new ConfigViewModel(
            new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(), () => Task.FromResult<string?>(null),
            autoConnect, autostart);
        return (vm, autoConnect, autostart);
    }

    /// <summary>Обе радиокнопки группы «AutoConnectTarget» — только они, без чужих RadioButton.</summary>
    private static RadioButton[] TargetRadios(Window window) =>
        window.GetVisualDescendants().OfType<RadioButton>()
            .Where(r => r.GroupName == "AutoConnectTarget")
            .ToArray();

    // Content — реальная строка из локализации (как и Content каждого CheckBox/RadioButton
    // карточки), поэтому её можно использовать как надёжный опознавательный признак элемента,
    // а не полагаться на порядок в разметке.
    private static RadioButton LastRadio(Window window) =>
        TargetRadios(window).Single(r => Equals(r.Content, Localizer.Instance[LocKeys.Settings_AutoConnectLast]));

    private static RadioButton FastestRadio(Window window) =>
        TargetRadios(window).Single(r => Equals(r.Content, Localizer.Instance[LocKeys.Settings_AutoConnectFastest]));

    private static CheckBox AutoConnectCheckBox(Window window) =>
        window.GetVisualDescendants().OfType<CheckBox>()
            .Single(b => Equals(b.Content, Localizer.Instance[LocKeys.Settings_AutoConnect]));

    private static CheckBox AutostartCheckBox(Window window) =>
        window.GetVisualDescendants().OfType<CheckBox>()
            .Single(b => Equals(b.Content, Localizer.Instance[LocKeys.Settings_Autostart]));

    [AvaloniaFact]
    public void Startup_card_renders_both_radios_and_both_checkboxes()
    {
        var (vm, _, _) = VmWithStartup();
        var window = Show(vm);

        Assert.Equal(2, TargetRadios(window).Length);
        // Опознание по локализованному Content уже доказывает, что «последняя»/«самая быстрая»
        // — это именно те две радиокнопки, а не два произвольных RadioButton в группе.
        Assert.NotNull(LastRadio(window));
        Assert.NotNull(FastestRadio(window));
        Assert.NotNull(AutoConnectCheckBox(window));
        Assert.NotNull(AutostartCheckBox(window));
    }

    [AvaloniaFact]
    public void Radio_enablement_gate_is_live_in_the_rendered_tree()
    {
        var (vm, _, _) = VmWithStartup();
        Assert.False(vm.AutoConnectEnabled);   // свежий стор — гейт закрыт с самого начала
        var window = Show(vm);

        var radios = TargetRadios(window);
        Assert.Equal(2, radios.Length);
        // Гейт закрыт: обе радиокнопки эффективно недоступны, хотя сами по себе видимы.
        Assert.All(radios, r => Assert.False(r.IsEffectivelyEnabled));

        vm.AutoConnectEnabled = true;

        Assert.All(radios, r => Assert.True(r.IsEffectivelyEnabled));

        vm.AutoConnectEnabled = false;

        Assert.All(radios, r => Assert.False(r.IsEffectivelyEnabled));
    }

    [AvaloniaFact]
    public void Toggling_the_fastest_radio_through_the_rendered_control_persists_to_the_store()
    {
        var (vm, autoConnect, _) = VmWithStartup();
        var window = Show(vm);
        vm.AutoConnectEnabled = true;

        var fastestRadio = FastestRadio(window);
        var lastUsedRadio = LastRadio(window);

        fastestRadio.IsChecked = true;

        Assert.True(vm.AutoConnectUseFastest);
        Assert.Equal(AutoConnectMode.Fastest, autoConnect.Load());

        lastUsedRadio.IsChecked = true;

        Assert.False(vm.AutoConnectUseFastest);
        Assert.Equal(AutoConnectMode.LastUsed, autoConnect.Load());
    }

    [AvaloniaFact]
    public void Toggling_the_autostart_checkbox_through_the_rendered_control_flips_the_service()
    {
        var (vm, _, autostart) = VmWithStartup();
        var window = Show(vm);
        Assert.False(autostart.IsEnabled());

        var box = AutostartCheckBox(window);

        box.IsChecked = true;

        Assert.True(vm.AutostartEnabled);
        Assert.True(autostart.IsEnabled());

        box.IsChecked = false;

        Assert.False(vm.AutostartEnabled);
        Assert.False(autostart.IsEnabled());
    }
}
