using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vantah.App.Services;
using Vantah.Core.Models;
using Vantah.Core.State;

/// <summary>
/// Правило «подключились — перечитать то, что не прочиталось». Триггер именно переход в
/// Connected: опрос статуса пишет Connected каждые несколько секунд, и реакция на само
/// значение перезапускала бы загрузку бесконечно.
/// </summary>
public class SectionReloaderTests
{
    private sealed class StubSection(string id) : IReloadableSection
    {
        public string Id => id;
        public bool LoadFailed { get; set; }
        public int Reloads { get; private set; }

        public Task ReloadIfFailedAsync()
        {
            if (!LoadFailed) return Task.CompletedTask;
            Reloads++;
            LoadFailed = false;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSection : IReloadableSection
    {
        public string Id => "throwing";
        public bool LoadFailed => true;
        public Task ReloadIfFailedAsync() => throw new InvalidOperationException("boom");
    }

    /// <summary>Запоминает, на каком потоке её реально вызвали — для проверки маршалинга.</summary>
    private sealed class ThreadRecordingSection : IReloadableSection
    {
        public string Id => "thread-recording";
        public bool LoadFailed => true;
        public bool? CalledOnUiThread { get; private set; }

        public Task ReloadIfFailedAsync()
        {
            CalledOnUiThread = Dispatcher.UIThread.CheckAccess();
            return Task.CompletedTask;
        }
    }

    private static void Connect(AppStateStore store) =>
        store.Set(s => s with { Connection = ConnectionState.Connected });

    private static void Disconnect(AppStateStore store) =>
        store.Set(s => s with { Connection = ConnectionState.Disconnected });

    [AvaloniaFact]
    public async Task Failed_sections_reload_when_the_vpn_connects()
    {
        var failed = new StubSection("failed") { LoadFailed = true };
        var healthy = new StubSection("healthy");
        var store = new AppStateStore();
        var reloader = new SectionReloader(store, [failed, healthy]);

        Connect(store);
        await reloader.LastRunTask;

        Assert.Equal(1, failed.Reloads);
        Assert.Equal(0, healthy.Reloads);
    }

    [AvaloniaFact]
    public async Task Repeated_connected_snapshots_do_not_reload_again()
    {
        var section = new StubSection("failed") { LoadFailed = true };
        var store = new AppStateStore();
        var reloader = new SectionReloader(store, [section]);

        Connect(store);
        await reloader.LastRunTask;
        section.LoadFailed = true;      // как если бы повтор тоже не удался
        Connect(store);                 // очередной тик опроса: состояние то же
        await reloader.LastRunTask;

        Assert.Equal(1, section.Reloads);
    }

    [AvaloniaFact]
    public async Task Reconnect_gives_a_new_attempt()
    {
        var section = new StubSection("failed") { LoadFailed = true };
        var store = new AppStateStore();
        var reloader = new SectionReloader(store, [section]);

        Connect(store);
        await reloader.LastRunTask;
        section.LoadFailed = true;
        Disconnect(store);
        Connect(store);
        await reloader.LastRunTask;

        Assert.Equal(2, section.Reloads);
    }

    // Сбой одного раздела не должен мешать остальным: у каждого своя ошибка на экране.
    [AvaloniaFact]
    public async Task A_throwing_section_does_not_stop_the_others()
    {
        var throwing = new ThrowingSection();
        var section = new StubSection("failed") { LoadFailed = true };
        var store = new AppStateStore();
        var reloader = new SectionReloader(store, [throwing, section]);

        Connect(store);
        await reloader.LastRunTask;

        Assert.Equal(1, section.Reloads);
    }

    /// <summary>
    /// В реальном приложении AppStateStore.Changed приходит с потока фонового опроса (см.
    /// VpnCoordinator), а ReloadIfFailedAsync правит привязанные к UI свойства — вызов обязан
    /// стартовать на UI-потоке независимо от того, с какого потока пришло Changed.
    /// </summary>
    [AvaloniaFact]
    public async Task Reload_starts_on_the_ui_thread_even_when_changed_fires_from_a_pool_thread()
    {
        var section = new ThreadRecordingSection();
        var store = new AppStateStore();
        var reloader = new SectionReloader(store, [section]);

        await Task.Run(() => Connect(store));   // Changed поднимается с потока пула
        await reloader.LastRunTask;

        Assert.True(section.CalledOnUiThread);
    }
}
