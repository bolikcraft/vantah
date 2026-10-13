using System.Threading.Channels;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Update;

/// <summary>
/// Проверка обновлений, пока Vantah живёт в трее: сразу при старте, потом раз в час.
/// Время ручное — тесты не ждут настоящих часов.
/// </summary>
public sealed class AppUpdateMonitorTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private static readonly AppUpdateInfo Release =
        new("v0.2.0", "https://github.com/bolikcraft/vantah/releases/tag/v0.2.0");

    private readonly CancellationTokenSource _stop = new();

    // Цикл монитора бесконечный: без отмены он пережил бы тест.
    public void Dispose() => _stop.Cancel();

    private static Func<AppUpdateInfo?> Returns(AppUpdateInfo? release) => () => release;

    private static Func<AppUpdateInfo?> Throws() => () => throw new HttpRequestException("network is down");

    /// <summary>Источник, который отвечает по сценарию; последний ответ повторяется.</summary>
    private sealed class ScriptedSource(params Func<AppUpdateInfo?>[] answers) : IAppReleaseSource
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public bool CalledOnUiThread { get; private set; }

        public Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default)
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

    private sealed record Rig(
        AppUpdateMonitor Monitor, ManualTime Time, ScriptedSource Source,
        AppUpdateService Service, UpdateBannerViewModel Banner, AboutViewModel About);

    private static Rig Build(ScriptedSource source, string? dismissed = null)
    {
        var path = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "appupdate.json");
        var store = new AppUpdateStore(path);
        if (dismissed is not null) store.Save(new AppUpdateState { DismissedVersion = dismissed });

        var service = new AppUpdateService(source, store, "0.1.0");
        var banner = new UpdateBannerViewModel(service);
        var about = new AboutViewModel(new FakeVpnService());
        var time = new ManualTime(Start);
        return new Rig(new AppUpdateMonitor(service, banner, about, time), time, source, service, banner, about);
    }

    [AvaloniaFact]
    public async Task First_check_runs_right_away_and_fills_banner_and_about()
    {
        var rig = Build(new ScriptedSource(Returns(Release)));

        _ = rig.Monitor.Start(_stop.Token);
        await rig.Time.NextSleepAsync();

        Assert.Equal(1, rig.Source.Calls);
        Assert.True(rig.Banner.IsVisible);
        Assert.Contains("0.2.0", rig.Banner.Text);
        Assert.True(rig.About.HasUpdate);
        Assert.Contains("0.2.0", rig.About.UpdateText);
    }

    [AvaloniaFact]
    public async Task Check_runs_off_the_ui_thread()
    {
        // Старт и автоподключение не должны ждать ни файла состояния, ни сети.
        var rig = Build(new ScriptedSource(Returns(Release)));

        _ = rig.Monitor.Start(_stop.Token);
        await rig.Time.NextSleepAsync();

        Assert.Equal(1, rig.Source.Calls);
        Assert.False(rig.Source.CalledOnUiThread);
    }

    [AvaloniaFact]
    public async Task Next_check_comes_an_hour_later()
    {
        // При старте сети не было, через час она появилась.
        var rig = Build(new ScriptedSource(Returns(null), Returns(Release)));

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();

        Assert.Equal(TimeSpan.FromHours(1), sleep.DueTime);
        Assert.False(rig.Banner.IsVisible);

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.Equal(2, rig.Source.Calls);
        Assert.True(rig.Banner.IsVisible);
        Assert.True(rig.About.HasUpdate);
    }

    [AvaloniaFact]
    public async Task Hourly_ticks_reach_the_network_only_once_a_day()
    {
        var rig = Build(new ScriptedSource(Returns(Release)));

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();
        for (var hour = 1; hour < 24; hour++)
        {
            rig.Time.Wake(sleep);
            sleep = await rig.Time.NextSleepAsync();
        }

        Assert.Equal(1, rig.Source.Calls);
        // Повторный показ той же версии плашку не ломает.
        Assert.True(rig.Banner.IsVisible);
        Assert.Contains("0.2.0", rig.Banner.Text);

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.Equal(2, rig.Source.Calls);
    }

    [AvaloniaFact]
    public async Task Exception_in_one_check_does_not_stop_the_loop()
    {
        var rig = Build(new ScriptedSource(Throws(), Returns(Release)));

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();

        Assert.False(rig.Banner.IsVisible);

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.True(rig.Banner.IsVisible);
    }

    [AvaloniaFact]
    public async Task Dismissed_version_skips_the_banner_but_reaches_about()
    {
        var rig = Build(new ScriptedSource(Returns(Release)), dismissed: "v0.2.0");

        _ = rig.Monitor.Start(_stop.Token);
        await rig.Time.NextSleepAsync();

        Assert.False(rig.Banner.IsVisible);
        Assert.True(rig.About.HasUpdate);
        Assert.Contains("0.2.0", rig.About.UpdateText);
    }

    [AvaloniaFact]
    public async Task Banner_closed_with_the_cross_stays_closed_on_later_ticks()
    {
        var rig = Build(new ScriptedSource(Returns(Release)));

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();
        rig.Banner.DismissCommand.Execute(null);

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.False(rig.Banner.IsVisible);
        Assert.True(rig.About.HasUpdate);
    }

    [AvaloniaFact]
    public async Task Check_without_an_update_clears_about_but_keeps_the_banner()
    {
        var rig = Build(new ScriptedSource(Returns(Release)));

        _ = rig.Monitor.Start(_stop.Token);
        var sleep = await rig.Time.NextSleepAsync();
        rig.Service.Enabled = false;   // с выключенной проверкой CheckAsync отвечает null

        rig.Time.Wake(sleep);
        await rig.Time.NextSleepAsync();

        Assert.False(rig.About.HasUpdate);
        Assert.True(rig.Banner.IsVisible);
    }
}
