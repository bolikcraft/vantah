using Avalonia;
using System;
using Vantah.App.ViewModels;
using Vantah.Core.Config;
using Vantah.Core.Startup;

namespace Vantah.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        var options = StartupOptions.Parse(args);
        // Справка и версия — консольный вывод, окно не создаётся.
        if (options.Help) { Console.WriteLine(StartupOptions.HelpText); return 0; }
        if (options.Version) { Console.WriteLine(AboutViewModel.CurrentVersion); return 0; }

        // Второй экземпляр не запускаем: он дал бы вторую иконку в трее, второй опрос статуса
        // и две записи в конфиг CLI одновременно. Вместо этого просим первый показать окно.
        var role = SingleInstance.Acquire(VantahPaths.RuntimeDir, out var instance);
        if (role == InstanceRole.Duplicate) return 0;

        var unguarded = role == InstanceRole.Unguarded;
        // Замок недоступен, а живого экземпляра за ним нет. Запускаемся без защиты, но говорим
        // об этом: иначе причина не видна вообще нигде. Консольный вывод не переводим.
        if (unguarded)
            Console.Error.WriteLine(
                $"vantah: cannot lock {VantahPaths.RuntimeDir} and no running instance answers; " +
                "starting without the single-instance guard");

        using (instance)
            BuildAvaloniaApp(options, instance, unguarded).StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(StartupOptions.Default, null, false);

    private static AppBuilder BuildAvaloniaApp(StartupOptions options, SingleInstance? instance, bool unguarded)
        => AppBuilder.Configure(() => new App(options, instance, unguarded))
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
