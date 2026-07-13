using Avalonia;
using Avalonia.Headless;
using Vantah.App;

[assembly: AvaloniaTestApplication(typeof(Vantah.App.Tests.AvaloniaTestApp))]

namespace Vantah.App.Tests;

public static class AvaloniaTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
