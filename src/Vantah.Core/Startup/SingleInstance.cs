// Vantah — только Linux, поэтому Unix-права доступны всегда (CA1416 предупреждает про Windows).
#pragma warning disable CA1416

using System.Net.Sockets;
using System.Text;

namespace Vantah.Core.Startup;

/// <summary>Роль процесса относительно замка единственного экземпляра.</summary>
public enum InstanceRole
{
    /// <summary>Замок наш — работаем дальше как единственный экземпляр.</summary>
    Owner,

    /// <summary>Замок у живого экземпляра, окно показал он — этому процессу пора выйти.</summary>
    Duplicate,

    /// <summary>Замок недоступен, и ответить по сокету некому — работаем без защиты от дубля.</summary>
    Unguarded,
}

/// <summary>
/// Замок единственного экземпляра: блокировка файла плюс unix-сокет рядом с ним. Первый процесс
/// держит замок и слушает сокет, второй по этому же сокету просит показать окно и выходит.
///
/// Замок — именно блокировка открытого файла (flock), а не «файл существует»: ядро снимает её
/// само, когда процесс умирает, поэтому осиротевший после kill -9 файл запуск не блокирует.
/// Осиротевший сокет тоже не мешает — перед bind мы его удаляем.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string LockFileName = "instance.lock";
    private const string SocketFileName = "instance.sock";

    /// <summary>Сообщение второго экземпляра. Читается целиком, поэтому нужен и разделитель.</summary>
    private const string ActivateMessage = "show\n";

    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode PrivateDir =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>Сколько ждём ответа уже запущенного экземпляра — он мог ещё не открыть сокет.</summary>
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Пауза между попытками достучаться до сокета.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly FileStream _lockFile;
    private readonly string _socketPath;
    private readonly CancellationTokenSource _cts = new();

    private Socket? _listener;

    private SingleInstance(FileStream lockFile, string socketPath)
    {
        _lockFile = lockFile;
        _socketPath = socketPath;
    }

    /// <summary>
    /// Разбирается, кто мы: владелец замка, лишний запуск или процесс без защиты. Последнее —
    /// когда замок взять не вышло, а достучаться не до кого: каталог недоступен на запись, кривые
    /// права, кончилось место. Молча выйти в этом случае нельзя — пользователь щёлкнул по значку
    /// и не получил ровно ничего; видимое окно без защиты от дубля лучше тишины.
    /// </summary>
    /// <param name="instance">Замок — только у <see cref="InstanceRole.Owner"/>, иначе null.</param>
    public static InstanceRole Acquire(string directory, out SingleInstance? instance, TimeSpan? signalTimeout = null)
    {
        instance = TryAcquire(directory);
        if (instance is not null) return InstanceRole.Owner;
        return TrySignal(directory, signalTimeout) ? InstanceRole.Duplicate : InstanceRole.Unguarded;
    }

    /// <summary>
    /// Пытается стать единственным экземпляром. null — замок занят живым процессом либо взять его
    /// не удалось вовсе; разбирает эти случаи <see cref="Acquire"/>.
    /// </summary>
    public static SingleInstance? TryAcquire(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory, PrivateDir);
            var path = Path.Combine(directory, LockFileName);
            // FileShare.None на Unix — это flock(LOCK_EX|LOCK_NB): занятый замок даёт IOException.
            var stream = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                UnixCreateMode = PrivateFile,
            });
            // Файл от прежней версии мог создаться с правами по umask — поджимаем.
            try { File.SetUnixFileMode(path, PrivateFile); } catch { /* не критично */ }

            // PID — для диагностики: по нему видно, кто держит замок.
            stream.SetLength(0);
            stream.Write(Encoding.UTF8.GetBytes(Environment.ProcessId.ToString()));
            stream.Flush();

            return new SingleInstance(stream, Path.Combine(directory, SocketFileName));
        }
        catch (IOException)
        {
            // Замок занят — это и есть «уже запущены». Прочие сбои (нет каталога, нет прав)
            // сюда тоже попадают: запускать второе окно из-за них хуже, чем один раз промолчать.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Открывает сокет и ждёт на нём просьбы показать окно. Колбэк зовётся с потока пула —
    /// работу с UI вызывающий обязан сам перевести на UI-поток.
    /// </summary>
    public void StartListening(Action onActivate)
    {
        try
        {
            // Осиротевший файл сокета (после kill -9) занимает путь: bind поверх него не проходит.
            // Замок мы к этому моменту уже держим, значит живого слушателя за ним нет.
            File.Delete(_socketPath);

            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
            listener.Listen(4);
            try { File.SetUnixFileMode(_socketPath, PrivateFile); } catch { /* не критично */ }
            _listener = listener;
            _ = AcceptLoopAsync(listener, onActivate);
        }
        catch
        {
            // Без сокета остаётся сам замок: второй запуск просто тихо выйдет, не показав окно.
        }
    }

    /// <summary>
    /// Просит уже запущенный экземпляр показать окно. false — достучаться не удалось (процесс
    /// умирает, сокета нет); вызывающему всё равно остаётся только выйти.
    /// </summary>
    public static bool TrySignal(string directory, TimeSpan? timeout = null)
    {
        var path = Path.Combine(directory, SocketFileName);
        var endpoint = new UnixDomainSocketEndPoint(path);
        // Повторяем до таймаута: замок берётся раньше, чем открывается сокет, и второй запуск
        // может прийти ровно в этот зазор.
        var deadline = DateTime.UtcNow + (timeout ?? SignalTimeout);
        while (true)
        {
            try
            {
                using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                socket.Connect(endpoint);
                socket.Send(Encoding.UTF8.GetBytes(ActivateMessage));
                return true;
            }
            catch
            {
                if (DateTime.UtcNow >= deadline) return false;
                Thread.Sleep(RetryDelay);
            }
        }
    }

    private async Task AcceptLoopAsync(Socket listener, Action onActivate)
    {
        var buffer = new byte[ActivateMessage.Length];
        while (!_cts.IsCancellationRequested)
        {
            Socket client;
            try { client = await listener.AcceptAsync(_cts.Token); }
            catch { return; }   // сокет закрыт или отменён — слушать больше нечего

            using (client)
            {
                try
                {
                    // Свой таймаут на чтение: молчащий клиент не должен занимать очередь навсегда.
                    using var read = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                    read.CancelAfter(SignalTimeout);
                    var count = await client.ReceiveAsync(buffer, SocketFlags.None, read.Token);
                    if (Encoding.UTF8.GetString(buffer, 0, count) == ActivateMessage) onActivate();
                }
                catch
                {
                    // Обрыв соединения или таймаут — ждём следующего клиента.
                }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener?.Dispose();
        try { File.Delete(_socketPath); } catch { /* уборка best-effort */ }
        // Файл замка НЕ удаляем: другой процесс мог уже открыть его и ждать блокировку, и
        // удаление подсунуло бы следующему запуску новый файл — замок стал бы двойным.
        _lockFile.Dispose();
        _cts.Dispose();
    }
}
