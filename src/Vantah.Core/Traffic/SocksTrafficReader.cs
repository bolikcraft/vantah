using System.Diagnostics;

namespace Vantah.Core.Traffic;

/// <summary>Счётчики трафика в режиме SOCKS: туннельного интерфейса нет, читаем сокеты демона.</summary>
public interface ISocksTrafficReader
{
    /// <summary>Накопленные байты (принято, отправлено) или null, если посчитать не удалось.</summary>
    (long rx, long tx)? Read(int socksPort);
}

/// <summary>
/// Складывает трафик соединений демона с сервером VPN. Значения накапливаем сами: соединение
/// может смениться (переподключение), и счётчики нового сокета начинаются с нуля — без
/// накопления показания прыгали бы назад.
/// </summary>
public sealed class SocksTrafficReader(Func<string?>? readSs = null) : ISocksTrafficReader
{
    private readonly Func<string?> _readSs = readSs ?? RunSs;
    private Dictionary<string, SocketBytes> _last = new();
    private long _rx;
    private long _tx;

    public (long rx, long tx)? Read(int socksPort)
    {
        var output = _readSs();
        if (output is null) return null;

        var pid = SsTrafficParser.FindDaemonPid(output, socksPort);
        if (pid is null) return null;

        var current = new Dictionary<string, SocketBytes>();
        foreach (var socket in SsTrafficParser.ParseTunnelSockets(output, pid.Value))
        {
            current[socket.Key] = socket;
            // Сокета не видели раньше — берём его счётчики целиком: соединение могло появиться
            // до запуска Vantah, как и tun0, чьи счётчики мы тоже читаем «с начала туннеля».
            var previous = _last.GetValueOrDefault(socket.Key);
            _rx += Delta(socket.Received, previous.Received);
            _tx += Delta(socket.Sent, previous.Sent);
        }

        // Пропавшие сокеты просто забываем: их байты уже в сумме, а ключ (пара адресов)
        // может достаться новому соединению — тогда его счётчики начнутся с нуля.
        _last = current;
        return (_rx, _tx);
    }

    // Счётчик сокета только растёт, поэтому значение меньше прошлого означает, что ключ достался
    // новому соединению, — такой сокет считаем целиком. Равенство (простой сокет) обязано давать
    // ноль: иначе каждый опрос добавлял бы весь счётчик заново и скорость улетала бы в космос.
    private static long Delta(long current, long previous) => current >= previous ? current - previous : current;

    private static string? RunSs()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ss")
            {
                // -t TCP, -i счётчики, -n без резолва имён, -p владелец сокета,
                // -a вместе со слушающими (по слушающему сокету находим демона).
                ArgumentList = { "-tinpa" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            return process.WaitForExit(3000) && process.ExitCode == 0 ? output : null;
        }
        catch
        {
            // ss может не стоять в системе — тогда трафик в режиме SOCKS просто не считаем.
            return null;
        }
    }
}
