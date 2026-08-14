using Vantah.App.Tray;

namespace Vantah.App.Tests.Tray;

/// <summary>
/// Правило «трей пересоздаём, когда org.kde.StatusNotifierWatcher появился на шине сигналом».
/// Сам D-Bus сюда не нужен: и паузу, и пересоздание правило получает делегатами — иначе
/// проверить его можно было бы только руками, блокируя экран GNOME.
/// </summary>
public class TrayRestartPolicyTests
{
    /// <summary>
    /// Пауза, которой управляет тест: считает входы и отпускает ожидающих по команде.
    /// Нужна, чтобы поймать состояние «пересоздание запланировано, но ещё не случилось» —
    /// именно в нём гасится дребезг сигналов.
    /// </summary>
    private sealed class Gate
    {
        private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Entered { get; private set; }

        public Task WaitAsync()
        {
            Entered++;
            return _tcs.Task;
        }

        public void Release()
        {
            var current = _tcs;
            _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            current.TrySetResult();
        }
    }

    private sealed class RebuildSpy
    {
        public int Calls { get; private set; }
        public bool Throw { get; set; }

        public Task RunAsync()
        {
            Calls++;
            return Throw ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask;
        }
    }

    private static (TrayRestartPolicy Policy, Gate Delay, RebuildSpy Rebuild) NewPolicy()
    {
        var delay = new Gate();
        var rebuild = new RebuildSpy();
        return (new TrayRestartPolicy(delay.WaitAsync, rebuild.RunAsync), delay, rebuild);
    }

    /// <summary>
    /// Автозапуск: Vantah поднимается раньше расширения GNOME, на шине watcher'а ещё нет.
    /// Появление расширения — это и есть та гонка, из-за которой иконок становится две,
    /// так что пересоздание обязательно. Раньше первое появление считалось штатной
    /// регистрацией и пропускалось — дубль оставался до перезахода в сеанс.
    /// </summary>
    [Fact]
    public async Task The_first_appearance_already_rebuilds()
    {
        var (policy, delay, rebuild) = NewPolicy();

        policy.OwnerChanged(hasOwner: true);

        Assert.Equal(1, delay.Entered);
        Assert.Equal(0, rebuild.Calls);

        delay.Release();
        await policy.LastRunTask;

        Assert.Equal(1, rebuild.Calls);
    }

    [Fact]
    public void Losing_the_owner_alone_does_not_rebuild()
    {
        var (policy, delay, rebuild) = NewPolicy();

        policy.OwnerChanged(hasOwner: false);

        Assert.Equal(0, delay.Entered);
        Assert.Equal(0, rebuild.Calls);
    }

    /// <summary>Главный сценарий: заблокировали экран (имя ушло) и разблокировали (вернулось).</summary>
    [Fact]
    public async Task A_return_after_a_loss_rebuilds_after_the_delay()
    {
        var (policy, delay, rebuild) = NewPolicy();

        policy.OwnerChanged(hasOwner: false);   // блокировка экрана
        policy.OwnerChanged(hasOwner: true);    // возврат

        // Пауза обязательна: пересоздание раньше неё попадёт в ту же гонку с расширением.
        Assert.Equal(1, delay.Entered);
        Assert.Equal(0, rebuild.Calls);

        delay.Release();
        await policy.LastRunTask;

        Assert.Equal(1, rebuild.Calls);
    }

    /// <summary>
    /// Расширение выгружается и грузится не по одному разу за блокировку — сигналов приходит
    /// пачка. Пересоздание должно быть одно: каждое лишнее моргает иконкой в панели.
    /// </summary>
    [Fact]
    public async Task A_burst_of_appearances_rebuilds_only_once()
    {
        var (policy, delay, rebuild) = NewPolicy();

        policy.OwnerChanged(hasOwner: false);
        policy.OwnerChanged(hasOwner: true);
        policy.OwnerChanged(hasOwner: false);
        policy.OwnerChanged(hasOwner: true);
        policy.OwnerChanged(hasOwner: true);

        Assert.Equal(1, delay.Entered);

        delay.Release();
        await policy.LastRunTask;

        Assert.Equal(1, rebuild.Calls);
    }

    /// <summary>Гашение дребезга не должно быть «на один раз»: следующая блокировка — новое пересоздание.</summary>
    [Fact]
    public async Task A_later_reappearance_rebuilds_again()
    {
        var (policy, delay, rebuild) = NewPolicy();

        policy.OwnerChanged(hasOwner: true);
        delay.Release();
        await policy.LastRunTask;

        policy.OwnerChanged(hasOwner: false);
        policy.OwnerChanged(hasOwner: true);
        delay.Release();
        await policy.LastRunTask;

        Assert.Equal(2, rebuild.Calls);
    }

    /// <summary>
    /// Пересоздание идёт по чужому коду (D-Bus, платформенный трей) и вполне может бросить.
    /// Ни ронять приложение, ни заклинивать правило навсегда это не должно.
    /// </summary>
    [Fact]
    public async Task A_failed_rebuild_does_not_wedge_the_policy()
    {
        var (policy, delay, rebuild) = NewPolicy();
        rebuild.Throw = true;

        policy.OwnerChanged(hasOwner: true);
        delay.Release();
        await policy.LastRunTask;   // исключение проглочено — ожидание не падает

        rebuild.Throw = false;
        policy.OwnerChanged(hasOwner: false);
        policy.OwnerChanged(hasOwner: true);
        delay.Release();
        await policy.LastRunTask;

        Assert.Equal(2, rebuild.Calls);
    }
}
