using Vantah.Core.Models;

namespace Vantah.Core.Traffic;

public sealed class TrafficMonitor(ITrafficReader reader, ISocksTrafficReader? socks = null)
{
    private long? _lastRx;
    private long? _lastTx;
    private string? _lastSource;

    public void Reset() { _lastRx = null; _lastTx = null; }

    /// <summary>Трафик туннеля — по счётчикам сетевого интерфейса.</summary>
    public TrafficSample? Poll(string iface, double elapsedSeconds) =>
        Sample($"iface:{iface}", reader.Read(iface), elapsedSeconds);

    /// <summary>
    /// Трафик в режиме SOCKS: интерфейса нет, считаем по соединениям демона с сервером VPN.
    /// Без такого читателя (его может не быть в тестах) остаёмся без данных, как раньше.
    /// </summary>
    public TrafficSample? PollSocks(int socksPort, double elapsedSeconds) =>
        socks is null ? null : Sample("socks", socks.Read(socksPort), elapsedSeconds);

    private TrafficSample? Sample(string source, (long rx, long tx)? read, double elapsedSeconds)
    {
        if (read is null) { Reset(); _lastSource = null; return null; }

        // Смена источника (переподключение tun0→tun1, переключение режима) обнуляет базу,
        // чтобы не посчитать фиктивную дельту между разными счётчиками.
        if (source != _lastSource) Reset();
        _lastSource = source;

        var (rx, tx) = read.Value;
        double rxRate = 0, txRate = 0;
        if (_lastRx is long lr && _lastTx is long lt && elapsedSeconds > 0)
        {
            rxRate = Math.Max(0, (rx - lr) / elapsedSeconds);
            txRate = Math.Max(0, (tx - lt) / elapsedSeconds);
        }
        _lastRx = rx; _lastTx = tx;
        return new TrafficSample(rx, tx, rxRate, txRate);
    }
}
