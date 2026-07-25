using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Autostart;
using Vantah.Core.Errors;
using Vantah.Core.Localization;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Вкладка «Настройки» до ответа `config show` не имеет права показывать форму: дефолты
/// вьюмодели пользователь принимает за свои настройки. Проверяем три состояния на настоящем
/// ConfigView в headless-окне — зелёная сборка про биндинги IsVisible ничего не доказывает.
/// </summary>
public class ConfigViewSkeletonTests
{
    // Свои временные пути для языка/автозапуска: прогон тестов не должен трогать
    // настоящие ~/.config/vantah/*.
    private static ConfigViewModel Vm(FakeConfigService svc)
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        return new ConfigViewModel(
            svc, new AppStateStore(),
            new LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            new AutoConnectStore(Path.Combine(root, "autoconnect")),
            new AutostartService(Path.Combine(root, "autostart"), "vantah", "vantah"),
            killSwitch: new KillSwitchStore(Path.Combine(root, "killswitch")));
    }

    private static Window Show(ConfigViewModel vm)
    {
        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 800, Height = 900 };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Control Named(Window w, string name) =>
        w.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);

    private static int SkeletonTiles(Window w) =>
        w.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("skeleton"));

    private static bool AnyVisibleCombo(Window w) =>
        w.GetVisualDescendants().OfType<ComboBox>().Any(c => c.IsEffectivelyVisible);

    [AvaloniaFact]
    public async Task Skeleton_stands_in_for_the_form_until_the_config_arrives()
    {
        var svc = new FakeConfigService();
        svc.HoldGet();
        var vm = Vm(svc);
        var window = Show(vm);

        Assert.True(vm.IsLoading);
        Assert.False(vm.IsLoaded);
        Assert.False(Named(window, "ConfigForm").IsEffectivelyVisible);
        Assert.False(AnyVisibleCombo(window));           // ни одного настоящего контрола формы
        var skeleton = Named(window, "ConfigSkeleton");
        Assert.True(skeleton.IsEffectivelyVisible);
        Assert.True(SkeletonTiles(window) > 0);
        // Силуэт тот же, что у формы: заглушки ложатся в две колонки, а не в столбик.
        Assert.Equal(2, skeleton.GetVisualDescendants().OfType<ContentPresenter>()
            .Select(p => p.Bounds.X).Distinct().Count());

        svc.ReleaseGet();
        await vm.LoadTask;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.True(vm.IsLoaded);
        Assert.False(Named(window, "ConfigSkeleton").IsEffectivelyVisible);
        Assert.True(Named(window, "ConfigForm").IsEffectivelyVisible);
        Assert.True(AnyVisibleCombo(window));
    }

    [AvaloniaFact]
    public async Task Failed_load_shows_the_error_and_refresh_recovers_the_form()
    {
        var svc = new FakeConfigService { GetError = new VpnCommandException(AppError.Cli("нет ответа от CLI")) };
        var vm = Vm(svc);
        await vm.LoadTask;
        var window = Show(vm);

        Assert.Equal("нет ответа от CLI", vm.LoadError);
        Assert.Null(vm.Error);                            // сбой загрузки — не ошибка действия
        Assert.False(vm.IsLoaded);
        Assert.False(Named(window, "ConfigForm").IsEffectivelyVisible);
        Assert.False(Named(window, "ConfigSkeleton").IsEffectivelyVisible);

        var block = Named(window, "ConfigLoadError");
        Assert.True(block.IsEffectivelyVisible);
        Assert.Contains(block.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text == "нет ответа от CLI" && t.IsEffectivelyVisible);

        // Жмём именно кнопку разметки, а не команду вьюмодели: проверяем проводку.
        var refresh = block.GetVisualDescendants().OfType<Button>()
            .Single(b => b.GetType() == typeof(Button) && b.IsEffectivelyVisible);
        svc.GetError = null;
        refresh.Command!.Execute(refresh.CommandParameter);
        await vm.LoadTask;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Null(vm.LoadError);
        Assert.True(vm.IsLoaded);
        Assert.False(Named(window, "ConfigLoadError").IsEffectivelyVisible);
        Assert.True(Named(window, "ConfigForm").IsEffectivelyVisible);
        Assert.True(AnyVisibleCombo(window));
        Assert.Equal(2, svc.Calls.Count(c => c == "get"));
    }
}
