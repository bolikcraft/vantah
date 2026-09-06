using System;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Vantah.App.Notifications;

/// <summary>
/// Плашки через org.freedesktop.Notifications на сессионной шине.
///
/// Своё соединение, а не то, на котором сидит трей: трей своё пересоздаёт при возврате
/// watcher'а (см. <see cref="Tray.StatusNotifierWatcherMonitor"/>), и уведомления умирали бы
/// вместе с ним.
///
/// Любой отказ (не Linux, нет адреса шины, сервиса уведомлений нет, он молчит) — это молчание,
/// а не исключение: приложение обязано работать и без уведомлений.
/// </summary>
public sealed class DBusDesktopNotifier : IDesktopNotifier, IDisposable
{
    private const string Service = "org.freedesktop.Notifications";
    private const string ObjectPath = "/org/freedesktop/Notifications";
    private const string AppName = "Vantah";

    /// <summary>Имя значка из темы — то же, что в packaging/vantah.desktop.</summary>
    private const string IconName = "vantah";

    /// <summary>Notify(app_name, replaces_id, app_icon, summary, body, actions, hints, expire_timeout).</summary>
    private const string NotifySignature = "susssasa{sv}i";

    /// <summary>−1 — время показа выбирает сам сервис уведомлений.</summary>
    private const int ServerDefaultTimeout = -1;

    /// <summary>Предел ожидания ответа шины: сервис уведомлений может и не ответить.</summary>
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private DBusConnection? _connection;

    /// <summary>Id предыдущей плашки: новая заменяет её, а не ложится сверху стопкой.</summary>
    private uint _replacesId;

    private bool _disposed;

    public async Task NotifyAsync(string summary, string body, CancellationToken ct = default)
    {
        if (_disposed) return;
        if (!OperatingSystem.IsLinux() || string.IsNullOrEmpty(DBusAddress.Session)) return;

        try
        {
            await SendAsync(summary, body, ct).WaitAsync(CallTimeout, ct);
        }
        catch
        {
            // Шины нет, сервиса нет, соединение отвалилось, ответа не дождались — плашки
            // просто не будет. Наружу это не выходит: приложение работает и без уведомлений.
        }
    }

    private async Task SendAsync(string summary, string body, CancellationToken ct)
    {
        // Всё, что трогает соединение и id предыдущей плашки, идёт под замком — включая сброс
        // соединения после сбоя: иначе ожидающая отправка осталась бы с закрытым соединением.
        await _gate.WaitAsync(ct);
        try
        {
            if (_disposed) return;
            try
            {
                var connection = _connection ??= await OpenAsync();
                _replacesId = await connection.CallMethodAsync(
                    Compose(connection, summary, body, _replacesId), ReadId);
            }
            catch
            {
                DropConnection();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<DBusConnection> OpenAsync()
    {
        var connection = new DBusConnection(new DBusConnectionOptions(DBusAddress.Session!)
        {
            AutoConnect = false,
        });
        try
        {
            await connection.ConnectAsync();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    // Тело сообщения собираем отдельным синхронным методом: MessageWriter — ref struct,
    // и жить через await он не может.
    private static MessageBuffer Compose(DBusConnection connection, string summary, string body, uint replacesId)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, ObjectPath, Service, "Notify", NotifySignature);
        writer.WriteString(AppName);
        writer.WriteUInt32(replacesId);
        writer.WriteString(IconName);
        writer.WriteString(summary);
        writer.WriteString(body);
        var actions = writer.WriteArrayStart(DBusType.String);
        writer.WriteArrayEnd(actions);
        var hints = writer.WriteDictionaryStart();
        writer.WriteDictionaryEnd(hints);
        writer.WriteInt32(ServerDefaultTimeout);
        return writer.CreateMessage();
    }

    // Ответ Notify — id показанной плашки; его же передаём в replaces_id следующей.
    private static uint ReadId(Message message, object? state) => message.GetBodyReader().ReadUInt32();

    // Зовётся под _gate (либо уже после _disposed): следующая отправка откроет соединение заново.
    private void DropConnection()
    {
        _connection?.Dispose();
        _connection = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DropConnection();
    }
}
