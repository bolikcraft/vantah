using System.Collections.Concurrent;
using System.Threading.Channels;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vantah.App.Services;
using Vantah.App.ViewModels;
using Vantah.Core.Update;

/// <summary>
/// Проверка обновления adguardvpn-cli, пока Vantah работает: сразу при старте, потом раз в час.
/// Время ручное — тесты не ждут настоящих часов.
/// </summary>
public sealed class CliUpdateMonitorTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly CancellationTokenSource _stop = new();

    // Цикл монитора бесконечный: без отмены он пережил бы тест.
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private static UpdateStatus Outdated(string? version) => new(IsLatest: false, LatestVersion: version);

    private static UpdateStatus Latest() => new(IsLatest: true, LatestVersion: null);

    private static Func<UpdateStatus> Throws() => () => throw new InvalidOperationException("cli is gone");

    /// <summary>Проверяльщик, который отвечает по сценарию; последний ответ повторяется.</summary>
    private sealed class ScriptedChecker(params Func<UpdateStatus>[] answers) : IUpdateChecker
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public bool CalledOnUiThread { get; private set; }

        public Task<UpdateStatus> CheckAsync(CancellationToken ct = default)
        {
            var call = Interlocked.Increment(ref _calls) - 1;
            if (Dispatcher.UIThread.CheckAccess()) CalledOnUiThread = true;
            return Task.FromResult(answers[Math.Min(call, answers.Length - 1)]());
        }
    }

    /// <summary>
    /// Часы теста: время стоит, пока тест его не сдвинет. Новый таймер значит, что монитор
    /// закончил проверку и уснул до следующего тика.
    /// </summary>
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private readonly Channel<Sleep> _sleeps = Channel.CreateUnbounded<Sleep>();
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var sleep = new Sleep(callback, state, dueTime);
            _sleeps.Writer.TryWrite(sleep);
            return sleep;
        }

        /// <summary>Дождаться, пока монитор уснёт, и выполнить то, что он отправил в UI.</summary>
        public async Task<Sleep> NextSleepAsync()
        {
            var sleep = await _sleeps.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Dispatcher.UIThread.RunJobs();
            return sleep;
        }

        /// <summary>Сдвинуть время на длину сна и разбудить монитор — с пула, как настоящий таймер.</summary>
        public void Wake(Sleep sleep)
        {
            _now += sleep.DueTime;
            _ = Task.Run(sleep.Fire);
        }

        public sealed class Sleep(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
        {
            public TimeSpan DueTime => dueTime;

            public void Fire() => callback(state);

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose() { }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    // Сервису нужен источник релизов, но монитор зовёт у него только тумблер Enabled.
    private sealed class StubSource : IAppReleaseSource
    {
        public Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default) =>
            Task.FromResult<AppUpdateInfo?>(null);
    }

    private sealed class StubUpdater(UpdateResult result) : ICliUpdater
    {
        public Task<UpdateResult> UpdateAsync(CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private sealed record Rig(
        CliUpdateMonitor Monitor, ManualTime Time, ScriptedChecker Checker,
        AppUpdateService Service, CliUpdateBannerViewModel Banner);

    private static Rig Build(ScriptedChecker checker, string? dismissed = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var cliStore = new CliUpdateStore(Path.Combine(root, "cliupdate.json"));
        cliStore.Update(_ => new CliUpdateState { DismissedVersion = dismissed });

        var appStore = new AppUpdateStore(Path.Combine(root, "appupdate.json"));
        var service = new AppUpdateService(new StubSource(), appStore, "0.1.0");
        var banner = new CliUpdateBannerViewModel(
            new StubUpdater(new UpdateResult(UpdateOutcome.Updated, "")), cliStore);
        var time = new ManualTime(Start);
        return new Rig(new CliUpdateMonitor(checker, banner, service, cliStore, time), time, checker, service, banner);
    }

    [AvaloniaFact]
    public async Task First_check_runs_right_away_and_shows_the_banner()
    {
        var rig = Build(new ScriptedChecker(() => Outdated("2.3.1")));

        _ = rig.Monitor.Start(_stop.Token);
        await rig.Time.NextSleepAsync();

        Assert.Equal(1, rig.Checker.Calls);
        Assert.True(rig.Banner.IsVisible);
        Assert.Contains("2.3.1", rig.Banner.Text);
    }

    [AvaloniaFact]
    public async Task Next_check_comes_an_hour_later()
    {
        var rig = Build(new ScriptedChecker(() => Outdated("2.3.1")));

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();

        Assert.Equal(TimeSpan.FromHours(1), sleep.DueTime);

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.Equal(2, rig.Checker.Calls);
    }

    [AvaloniaFact]
    public async Task Disabled_toggle_skips_the_check_entirely()
    {
        var rig = Build(new ScriptedChecker(() => Outdated("2.3.1")));
        rig.Service.Enabled = false;

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();

        Assert.Equal(0, rig.Checker.Calls);
        Assert.False(rig.Banner.IsVisible);
        Assert.Equal(TimeSpan.FromHours(1), sleep.DueTime);

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.Equal(0, rig.Checker.Calls);
        Assert.False(rig.Banner.IsVisible);
    }

    [AvaloniaFact]
    public async Task Latest_result_hides_a_shown_banner()
    {
        // Пока приложение жило в трее, CLI обновили руками через пакетный менеджер.
        var rig = Build(new ScriptedChecker(() => Outdated("2.3.1"), () => Latest()));

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();
        Assert.True(rig.Banner.IsVisible);

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.False(rig.Banner.IsVisible);
    }

    [AvaloniaFact]
    public async Task Dismissed_version_is_not_reshown()
    {
        // Крестик спрятал 2.3.1 навсегда, а следующий релиз должен снова показаться.
        var rig = Build(
            new ScriptedChecker(() => Outdated("2.3.1"), () => Outdated("2.4.0")),
            dismissed: "2.3.1");

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();
        Assert.False(rig.Banner.IsVisible);

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.True(rig.Banner.IsVisible);
        Assert.Contains("2.4.0", rig.Banner.Text);
    }

    [AvaloniaFact]
    public async Task Versionless_outdated_status_shows_the_banner_without_a_version()
    {
        // Парсер не нашёл версию в выводе CLI — плашка всё равно нужна.
        var rig = Build(new ScriptedChecker(() => Outdated(null)));

        _ = rig.Monitor.Start(_stop.Token);
        await rig.Time.NextSleepAsync();

        Assert.True(rig.Banner.IsVisible);
        Assert.DoesNotContain("{0}", rig.Banner.Text);
        Assert.Contains("AdGuard VPN", rig.Banner.Text);
    }

    [AvaloniaFact]
    public async Task Exception_in_one_check_does_not_stop_the_loop()
    {
        var rig = Build(new ScriptedChecker(Throws(), () => Outdated("2.3.1")));

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();
        Assert.False(rig.Banner.IsVisible);

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.True(rig.Banner.IsVisible);
    }

    [AvaloniaFact]
    public async Task Results_reach_the_banner_on_the_ui_thread()
    {
        // Крестик жмут на UI-потоке. Покажи монитор плашку с фона — проверка скрытой версии
        // разошлась бы с нажатием, и закрытая плашка всплыла бы снова.
        var rig = Build(new ScriptedChecker(() => Outdated("2.3.1")));
        var changes = new ConcurrentQueue<(string Property, bool OnUiThread)>();
        rig.Banner.PropertyChanged += (_, e) =>
            changes.Enqueue((e.PropertyName ?? "", Dispatcher.UIThread.CheckAccess()));

        _ = rig.Monitor.Start(_stop.Token);
        await rig.Time.NextSleepAsync();

        Assert.Contains(changes, c => c.Property == nameof(CliUpdateBannerViewModel.IsVisible));
        Assert.All(changes, c => Assert.True(c.OnUiThread, $"{c.Property} изменено не на UI-потоке"));
    }
}
