// Vantah — только Linux, поэтому Unix-права доступны всегда (CA1416 предупреждает про Windows).
#pragma warning disable CA1416

using Vantah.Core.Startup;
using Xunit;

namespace Vantah.Core.Tests.Startup;

public class SingleInstanceTests
{
    private const UnixFileMode Private600 = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    // Путь unix-сокета ограничен 108 байтами, поэтому каталог короткий — не под GetTempPath()/guid.
    private static string TempDir() =>
        Path.Combine("/tmp", "vantah-si", Guid.NewGuid().ToString("N")[..8]);

    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(300);

    [Fact]
    public void First_instance_takes_the_lock_and_the_second_does_not()
    {
        var dir = TempDir();
        using var first = SingleInstance.TryAcquire(dir);

        Assert.NotNull(first);
        Assert.Null(SingleInstance.TryAcquire(dir));
    }

    [Fact]
    public void The_lock_is_released_on_dispose()
    {
        var dir = TempDir();
        SingleInstance.TryAcquire(dir)!.Dispose();

        using var second = SingleInstance.TryAcquire(dir);

        Assert.NotNull(second);
    }

    [Fact]
    public void Lock_file_is_private()
    {
        var dir = TempDir();
        using var instance = SingleInstance.TryAcquire(dir);

        Assert.Equal(Private600, File.GetUnixFileMode(Path.Combine(dir, "instance.lock")));
    }

    /// <summary>
    /// kill -9 оставляет и файл замка, и файл сокета. Блокировку ядро снимает само, а сокет
    /// надо удалить перед bind — иначе следующий запуск остался бы без связи навсегда.
    /// </summary>
    [Fact]
    public void Orphaned_lock_and_socket_files_do_not_block_the_next_start()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "instance.lock"), "999999");
        File.WriteAllText(Path.Combine(dir, "instance.sock"), "мусор от упавшего процесса");

        using var instance = SingleInstance.TryAcquire(dir);
        Assert.NotNull(instance);

        var activated = new ManualResetEventSlim();
        instance!.StartListening(activated.Set);

        Assert.True(SingleInstance.TrySignal(dir, Short));
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void A_signal_reaches_the_running_instance()
    {
        var dir = TempDir();
        using var instance = SingleInstance.TryAcquire(dir);
        var activated = new ManualResetEventSlim();
        instance!.StartListening(activated.Set);

        Assert.True(SingleInstance.TrySignal(dir, Short));

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Repeated_signals_are_all_delivered()
    {
        var dir = TempDir();
        using var instance = SingleInstance.TryAcquire(dir);
        var count = 0;
        var second = new ManualResetEventSlim();
        instance!.StartListening(() => { if (Interlocked.Increment(ref count) == 2) second.Set(); });

        Assert.True(SingleInstance.TrySignal(dir, Short));
        Assert.True(SingleInstance.TrySignal(dir, Short));

        Assert.True(second.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void A_signal_without_a_listener_fails_instead_of_hanging()
    {
        Assert.False(SingleInstance.TrySignal(TempDir(), Short));
    }

    [Fact]
    public void Acquire_reports_the_owner_of_a_free_lock()
    {
        Assert.Equal(InstanceRole.Owner, SingleInstance.Acquire(TempDir(), out var instance, Short));

        using (instance) Assert.NotNull(instance);
    }

    [Fact]
    public void Acquire_reports_a_duplicate_when_the_running_instance_answers()
    {
        var dir = TempDir();
        using var first = SingleInstance.TryAcquire(dir);
        var activated = new ManualResetEventSlim();
        first!.StartListening(activated.Set);

        var role = SingleInstance.Acquire(dir, out var second, Short);

        Assert.Equal(InstanceRole.Duplicate, role);
        Assert.Null(second);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// Каталог замка недоступен (здесь на его месте файл), и отвечать по сокету некому. Молча
    /// выйти нельзя: пользователь щёлкнул по значку и не получил ничего. Запускаемся без защиты.
    /// </summary>
    [Fact]
    public void Acquire_falls_back_to_unguarded_when_the_lock_directory_is_unusable()
    {
        var path = Path.Combine("/tmp", "vantah-si", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "не каталог");

        var role = SingleInstance.Acquire(path, out var instance, Short);

        Assert.Equal(InstanceRole.Unguarded, role);
        Assert.Null(instance);
    }

    // Замок занят, но слушателя за ним нет (сокет не открылся). Показать окно всё равно некому,
    // поэтому запускаемся без защиты — тишина хуже лишнего окна.
    [Fact]
    public void Acquire_falls_back_to_unguarded_when_the_lock_holder_does_not_listen()
    {
        var dir = TempDir();
        using var first = SingleInstance.TryAcquire(dir);

        var role = SingleInstance.Acquire(dir, out var second, Short);

        Assert.Equal(InstanceRole.Unguarded, role);
        Assert.Null(second);
    }

    [Fact]
    public void Dispose_removes_the_socket_file()
    {
        var dir = TempDir();
        var instance = SingleInstance.TryAcquire(dir);
        instance!.StartListening(() => { });
        var socket = Path.Combine(dir, "instance.sock");
        Assert.True(File.Exists(socket));

        instance.Dispose();

        Assert.False(File.Exists(socket));
    }
}
