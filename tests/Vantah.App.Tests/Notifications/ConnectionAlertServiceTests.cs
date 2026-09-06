using System.Collections.Concurrent;
using Vantah.App.Localization;
using Vantah.App.Notifications;
using Vantah.Core.Notifications;
using Xunit;

namespace Vantah.App.Tests.Notifications;

/// <summary>
/// Склейка «решение → плашка»: что уходит отправителю, а что не уходит вовсе. Отправку
/// подменяем дублем — живой сессионной шины в тестах нет и быть не должно.
/// </summary>
public class ConnectionAlertServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private static string TempSettings() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "notifications");

    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = Start;
        public void Tick() => Now += TimeSpan.FromSeconds(4);
    }

    private static (ConnectionAlertService Service, FakeNotifier Notifier, Clock Clock, NotificationsStore Store) Make()
    {
        var notifier = new FakeNotifier();
        var clock = new Clock();
        var store = new NotificationsStore(TempSettings());
        return (new ConnectionAlertService(notifier, store, clock: () => clock.Now), notifier, clock, store);
    }

    /// <summary>Прогоняет ленту опросов и дожидается фоновых отправок.</summary>
    private static async Task PollAsync(ConnectionAlertService service, Clock clock, LinkEvent link, int times)
    {
        for (var i = 0; i < times; i++)
        {
            service.Observe(link);
            clock.Tick();
            await service.LastNotifyTask;
        }
    }

    [Fact]
    public async Task Loss_and_recovery_reach_the_notifier_with_translated_text()
    {
        var (service, notifier, clock, _) = Make();
        await PollAsync(service, clock, LinkEvent.Up, 3);

        await PollAsync(service, clock, LinkEvent.Retrying, 5);
        await PollAsync(service, clock, LinkEvent.Up, 5);

        var loc = Localizer.Instance;
        Assert.Equal(
        [
            (loc[LocKeys.Notify_ConnectionLost], loc[LocKeys.Notify_ConnectionLostBody]),
            (loc[LocKeys.Notify_ConnectionRestored], loc[LocKeys.Notify_ConnectionRestoredBody]),
        ], notifier.Sent);
        // Ключ вместо строки означал бы забытый перевод — плашка показала бы «Notify_…».
        Assert.All(notifier.Sent, s =>
        {
            Assert.DoesNotContain("Notify_", s.Summary);
            Assert.DoesNotContain("Notify_", s.Body);
        });
    }

    [Fact]
    public async Task Disabled_toggle_never_touches_the_notifier()
    {
        var (service, notifier, clock, store) = Make();
        store.Save(false);

        await PollAsync(service, clock, LinkEvent.Up, 3);
        await PollAsync(service, clock, LinkEvent.Retrying, 10);
        await PollAsync(service, clock, LinkEvent.Up, 10);

        Assert.Empty(notifier.Sent);
    }

    /// <summary>Тумблер читается на каждом событии: выключение действует сразу, без перезапуска.</summary>
    [Fact]
    public async Task Toggle_switched_off_between_events_stops_the_next_plate()
    {
        var (service, notifier, clock, store) = Make();
        await PollAsync(service, clock, LinkEvent.Up, 3);
        await PollAsync(service, clock, LinkEvent.Retrying, 5);
        Assert.Single(notifier.Sent);

        store.Save(false);
        await PollAsync(service, clock, LinkEvent.Up, 5);

        Assert.Single(notifier.Sent);
    }

    [Fact]
    public async Task User_disconnect_sends_nothing()
    {
        var (service, notifier, clock, _) = Make();
        await PollAsync(service, clock, LinkEvent.Up, 3);

        service.Observe(LinkEvent.UserDisconnect);
        await service.LastNotifyTask;
        await PollAsync(service, clock, LinkEvent.Down, 10);

        Assert.Empty(notifier.Sent);
    }

    /// <summary>Отправитель не должен блокировать вызывающего: Observe зовут из опроса статуса.</summary>
    [Fact]
    public void Observe_returns_before_the_notifier_finishes()
    {
        var notifier = new BlockingNotifier();
        var clock = new Clock();
        var service = new ConnectionAlertService(
            notifier, new NotificationsStore(TempSettings()), clock: () => clock.Now);

        for (var i = 0; i < 3; i++) { service.Observe(LinkEvent.Up); clock.Tick(); }
        for (var i = 0; i < 5; i++) { service.Observe(LinkEvent.Retrying); clock.Tick(); }

        Assert.True(notifier.Entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            "отправка так и не началась");
        Assert.False(service.LastNotifyTask.IsCompleted);
        notifier.Release.Set();
    }
}

/// <summary>Дубль отправителя: записывает пары «заголовок, тело».</summary>
internal sealed class FakeNotifier : IDesktopNotifier
{
    private readonly ConcurrentQueue<(string Summary, string Body)> _sent = new();

    public IReadOnlyList<(string Summary, string Body)> Sent => _sent.ToArray();

    public Task NotifyAsync(string summary, string body, CancellationToken ct = default)
    {
        _sent.Enqueue((summary, body));
        return Task.CompletedTask;
    }
}

/// <summary>Отправитель, который «висит» на шине, пока его не отпустят.</summary>
internal sealed class BlockingNotifier : IDesktopNotifier
{
    public ManualResetEventSlim Entered { get; } = new(false);
    public ManualResetEventSlim Release { get; } = new(false);

    public Task NotifyAsync(string summary, string body, CancellationToken ct = default)
    {
        Entered.Set();
        Release.Wait(TimeSpan.FromSeconds(10));
        return Task.CompletedTask;
    }
}
