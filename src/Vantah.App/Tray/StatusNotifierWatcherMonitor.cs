using System;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Vantah.App.Tray;

/// <summary>
/// Следит на сессионной шине за владельцем org.kde.StatusNotifierWatcher и по его возврату
/// просит пересоздать трей (правило — в <see cref="TrayRestartPolicy"/>).
///
/// Отдельное соединение, а не то, на котором сидит трей: соединение трея мы как раз закрываем
/// при пересоздании, и наблюдение вместе с ним умерло бы после первого же срабатывания.
///
/// Только Linux и только при доступной сессионной шине; в любом другом случае TryStart вернёт
/// null и приложение просто останется без обхода.
/// </summary>
internal sealed class StatusNotifierWatcherMonitor : IDisposable
{
    private const string WatcherName = "org.kde.StatusNotifierWatcher";
    private const string DBusService = "org.freedesktop.DBus";

    /// <summary>
    /// Пауза между возвратом watcher'а и пересозданием. Пять секунд — с запасом: к этому
    /// моменту расширение GNOME доделывает скан шины, а Avalonia — свою перерегистрацию.
    /// Пересоздавать раньше бессмысленно, мы попадём в ту же гонку.
    /// </summary>
    private static readonly TimeSpan RebuildDelay = TimeSpan.FromSeconds(5);

    /// <summary>Пауза перед переподключением к шине после обрыва соединения.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly TrayRestartPolicy _policy;
    private readonly CancellationTokenSource _cts = new();

    private DBusConnection? _connection;
    private IDisposable? _subscription;
    private volatile bool _disposed;
    private int _restarting;

    private StatusNotifierWatcherMonitor(Func<Task> rebuild)
    {
        _policy = new TrayRestartPolicy(() => Task.Delay(RebuildDelay, _cts.Token), rebuild);
    }

    /// <summary>Текущее/последнее пересоздание — точка ожидания для тестов и диагностики.</summary>
    internal Task LastRunTask => _policy.LastRunTask;

    /// <summary>
    /// Запускает наблюдение. Возвращает null там, где наблюдать нечего (не Linux, нет адреса
    /// сессионной шины). Ссылку на результат нужно держать: она же и владеет соединением,
    /// и её же надо освободить на выходе из приложения.
    /// </summary>
    public static StatusNotifierWatcherMonitor? TryStart(Func<Task> rebuild)
    {
        if (!OperatingSystem.IsLinux()) return null;
        if (string.IsNullOrEmpty(DBusAddress.Session)) return null;

        var monitor = new StatusNotifierWatcherMonitor(rebuild);
        _ = monitor.RunAsync();
        return monitor;
    }

    private async Task RunAsync()
    {
        try
        {
            var connection = new DBusConnection(new DBusConnectionOptions(DBusAddress.Session!)
            {
                AutoConnect = false,
            });
            _connection = connection;
            await connection.ConnectAsync();
            // Dispose мог случиться, пока мы соединялись: он закрыл _connection ещё до того, как
            // мы его туда положили, — закрываем сами, иначе соединение переживёт приложение.
            if (_disposed) { connection.Dispose(); return; }

            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Sender = DBusService,
                Interface = DBusService,
                Member = "NameOwnerChanged",
                Arg0 = WatcherName,
            };

            // EmitOnConnectionClosed (в 0.94.1 то же самое, что устаревший EmitOnConnectionDispose):
            // при закрытии соединения — перезапуск dbus-daemon, обрыв — обработчик получает
            // завершающее уведомление без значения. Без флага подписка умирала бы молча, и обход
            // дубля переставал работать до перезапуска Vantah.
            var subscription = await connection.AddMatchAsync(
                rule, ReadNewOwner, OnOwnerChanged, emitOnCapturedContext: false,
                flags: ObserverFlags.EmitOnConnectionClosed);
            _subscription = subscription;
            if (_disposed) { subscription.Dispose(); return; }

            // Текущее состояние шины не спрашиваем: правило смотрит только на сигналы, а
            // владелец, уже сидевший на шине к этому моменту, зарегистрировал наш индикатор
            // ровно один раз — пересоздавать нечего.
        }
        catch
        {
            // Шины нет или она отказала — обход просто не работает. Ронять из-за этого
            // приложение нельзя: трей без обхода остаётся полностью рабочим.
        }
    }

    // Тело NameOwnerChanged — (sss): имя, прежний владелец, новый владелец. Пустая строка
    // в новом владельце значит «имя ушло с шины».
    private static bool ReadNewOwner(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        reader.ReadString();                       // name — отфильтровано правилом по arg0
        reader.ReadString();                       // oldOwner
        return reader.ReadString().Length > 0;     // newOwner
    }

    private void OnOwnerChanged(Notification<bool> notification)
    {
        if (_disposed) return;

        // Завершающее уведомление приходит без значения: соединение закрылось (перезапуск
        // dbus-daemon, обрыв) и подписки больше нет. Молча замолчать здесь нельзя — обход дубля
        // тогда не работал бы до перезапуска Vantah.
        if (!notification.HasValue) { _ = RestartAsync(); return; }

        _policy.OwnerChanged(notification.Value);
    }

    // Переподключение после обрыва. Одно за раз: закрытие старого соединения само присылает
    // завершающее уведомление, и без этого замка оно запустило бы следующий перезапуск.
    // Пауза — чтобы не молотить попытками, пока шина не поднялась; если новое соединение не
    // встанет, RunAsync тихо выйдет, и попыток больше не будет: следующее уведомление о
    // закрытии придёт только по живой подписке.
    private async Task RestartAsync()
    {
        if (Interlocked.Exchange(ref _restarting, 1) != 0) return;
        try
        {
            // Пауза первым делом: сюда нас позвал обработчик уведомления с потока D-Bus, и
            // закрывать его же соединение прямо из колбэка не стоит.
            await Task.Delay(RetryDelay, _cts.Token);
            if (_disposed) return;

            _subscription?.Dispose();
            _connection?.Dispose();
            _subscription = null;
            _connection = null;

            await RunAsync();
        }
        catch
        {
            // Отмена по Dispose или отказ шины — наблюдение просто заканчивается.
        }
        finally
        {
            Interlocked.Exchange(ref _restarting, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _subscription?.Dispose();
        _connection?.Dispose();
        _cts.Dispose();
    }
}
