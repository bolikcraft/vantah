using Avalonia;
using Avalonia.Headless;
using Vantah.App;

[assembly: AvaloniaTestApplication(typeof(Vantah.App.Tests.AvaloniaTestApp))]

// Тесты не параллелятся: headless-сессия Avalonia пересобирает изолированное приложение под
// каждый тест, и при параллельном запуске второй поток натыкается на композитор чужого
// диспетчера («The calling thread cannot access this object») либо прогон намертво зависает.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Vantah.App.Tests;

public static class AvaloniaTestApp
{
    /// <summary>
    /// Headless-приложение для UI-тестов. Рисуем через Skia (<c>UseHeadlessDrawing = false</c>),
    /// а не заглушкой: у заглушечного менеджера шрифтов все глифы квадратные (ширина = кегль),
    /// то есть текст почти вдвое шире настоящего. С такими метриками нельзя ни доказать, что
    /// подпись влезает, ни поверить, что она обрезана. Skia берёт тот же Inter, что и приложение.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
