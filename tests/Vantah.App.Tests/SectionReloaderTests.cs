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

    /// <summary>
    /// Раздел, чья перезагрузка не завершается, пока тест сам не отпустит её через Release() —
    /// нужен, чтобы держать прогон SectionReloader открытым и успеть подсунуть ему события,
    /// которые в обычных (синхронных) заглушках просто не успевают случиться до конца прогона.
    /// </summary>
    private sealed class GatedSection(string id) : IReloadableSection
    {
        private TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Id => id;
        public bool LoadFailed { get; set; }
        public int StartedCalls { get; private set; }
        public int Reloads { get; private set; }

        /// <summary>Дожидается момента, когда очередной вызов ReloadIfFailedAsync реально начался.</summary>
        public Task WaitUntilStarted() => _started.Task;

        public async Task ReloadIfFailedAsync()
        {
            if (!LoadFailed) return;
            StartedCalls++;
            _started.TrySetResult(true);
            await _release.Task;
            Reloads++;
            // LoadFailed НЕ сбрасываем сами (в отличие от StubSection): тест сам решает, была ли
            // очередная попытка успешной, выставляя LoadFailed до Release() — так сценарий может
            // явно показать и повторный провал (для проверки повтора), и итоговый успех.
        }

        /// <summary>
        /// Отпускает текущий прогон и сразу заводит свежие гейты для следующего возможного вызова —
        /// поле переставляется ДО пробуждения ожидающих Release(), поэтому WaitUntilStarted(),
        /// вызванный сразу после Release(), детерминированно ждёт именно СЛЕДУЮЩИЙ вызов.
        /// </summary>
        public void Release()
        {
            var release = _release;
            _release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            release.TrySetResult(true);
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

    // Раньше все заглушки завершались синхронно, и к моменту второго Connect прогон уже был
    // закончен — ветка single-flight (Interlocked.Exchange(ref _inFlight, 1) == 1) ни разу не
    // выполнялась в тестах. GatedSection держит прогон открытым и позволяет её проверить.
    [AvaloniaFact]
    public async Task Repeated_connected_snapshots_during_a_run_do_not_start_a_second_run()
    {
        var section = new GatedSection("gated") { LoadFailed = true };
        var store = new AppStateStore();
        var reloader = new SectionReloader(store, [section]);

        Connect(store);
        await section.WaitUntilStarted();

        // Опрос статуса продолжает слать Connected, пока прогон ещё не закончился — это НЕ
        // переход (previous уже Connected), второй прогон запускаться не должен.
        Connect(store);
        Connect(store);
        Assert.Equal(1, section.StartedCalls);

        section.Release();
        await reloader.LastRunTask;

        Assert.Equal(1, section.Reloads);
    }

    /// <summary>
    /// Главный тест на регресс: перезагрузка разделов может идти секунды (несколько вызовов
    /// CLI подряд). Если за это время случится НАСТОЯЩИЙ переход Disconnected→Connected, он не
    /// должен молча теряться — раздел, у которого LoadFailed снова true, обязан быть перечитан
    /// ещё раз сразу после завершения уже идущего прогона.
    /// </summary>
    [AvaloniaFact]
    public async Task A_connection_during_a_run_is_not_lost_and_triggers_a_retry()
    {
        var section = new GatedSection("gated") { LoadFailed = true };
        var store = new AppStateStore();
        var reloader = new SectionReloader(store, [section]);

        Connect(store);
        await section.WaitUntilStarted();      // первый прогон стартовал и встал на gate

        Disconnect(store);
        Connect(store);                        // настоящий переход, случившийся во время прогона

        // LoadFailed уже true (GatedSection сама его не сбрасывает) — как если бы повтор
        // тоже не удался, значит второй прогон обязан реально дёрнуть раздел ещё раз.
        section.Release();                     // отпускаем первый прогон
        await section.WaitUntilStarted();      // отложенный повтор должен стартовать сам,
                                                // без нового внешнего Connect
        Assert.Equal(2, section.StartedCalls);

        section.LoadFailed = false;            // на этот раз перечитать удалось
        section.Release();                     // отпускаем и второй прогон

        // LastRunTask обязана покрывать оба прогона — и исходный, и отложенный повтор.
        await reloader.LastRunTask;

        Assert.Equal(2, section.Reloads);
        Assert.False(section.LoadFailed);
    }
}
