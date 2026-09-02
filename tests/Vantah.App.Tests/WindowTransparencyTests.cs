using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Appearance;
using Vantah.Core.Localization;
using Vantah.Core.State;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Прозрачность окон: полупрозрачен только фон, и красит его одна общая кисть из ресурсов
/// приложения. Поэтому проверяем не каждое окно по отдельности, а что окна берут именно её,
/// что ползунок меняет её прозрачность и что значение переживает перезапуск.
/// </summary>
public class WindowTransparencyTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));

    private static SolidColorBrush SharedBrush()
    {
        Assert.True(Application.Current!.Resources.TryGetResource(
            WindowTransparency.BrushKey, null, out var value));
        return Assert.IsType<SolidColorBrush>(value);
    }

    private static ConfigViewModel NewConfigVm(string dir, WindowTransparency transparency) =>
        new(new FakeConfigService(), new AppStateStore(),
            new LanguageStore(Path.Combine(dir, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            transparency: transparency);

    [AvaloniaFact]
    public void Windows_paint_their_background_with_the_shared_brush()
    {
        var main = new MainWindow();
        var dialog = new DialogWindow(() => "Настройки", new TextBlock());
        main.Show();
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Same(SharedBrush(), main.Background);
        Assert.Same(SharedBrush(), dialog.Background);
    }

    /// <summary>Прозрачный визуал выбирается при создании окна, поэтому просить его надо заранее.</summary>
    [AvaloniaFact]
    public void Windows_ask_the_platform_for_transparency()
    {
        var main = new MainWindow();
        var dialog = new DialogWindow(() => "Настройки", new TextBlock());

        Assert.Contains(WindowTransparencyLevel.Transparent, main.TransparencyLevelHint);
        Assert.Contains(WindowTransparencyLevel.Transparent, dialog.TransparencyLevelHint);
    }

    [AvaloniaFact]
    public void The_slider_makes_windows_more_transparent_at_once()
    {
        var dir = TempDir();
        var store = new WindowOpacityStore(Path.Combine(dir, "window-opacity"));
        var vm = NewConfigVm(dir, new WindowTransparency(store));

        vm.WindowOpacity = 40;

        Assert.Equal(0.4, SharedBrush().Opacity, 3);
        Assert.Equal("40%", vm.WindowOpacityText);
    }

    [AvaloniaFact]
    public void The_chosen_opacity_survives_a_restart()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "window-opacity");
        var vm = NewConfigVm(dir, new WindowTransparency(new WindowOpacityStore(path)));

        vm.WindowOpacity = 65;

        // Новый запуск: своё хранилище, своя вьюмодель — ползунок обязан встать туда же.
        var restarted = NewConfigVm(dir, new WindowTransparency(new WindowOpacityStore(path)));
        Assert.Equal(65, restarted.WindowOpacity);
    }

    /// <summary>Загрузка формы не должна сама себя записывать в файл: значение ставится молча.</summary>
    [AvaloniaFact]
    public void Opening_the_settings_does_not_overwrite_the_stored_value()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "window-opacity");
        NewConfigVm(dir, new WindowTransparency(new WindowOpacityStore(path)));

        Assert.False(File.Exists(path));
    }
}
