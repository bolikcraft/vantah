using Vantah.Core.Models;

namespace Vantah.Core.Traffic;

public sealed class TrafficMonitor(ITrafficReader reader)
{
    private long? _lastRx;
    private long? _lastTx;
    private string? _lastIface;

    public void Reset() { _lastRx = null; _lastTx = null; }

    public TrafficSample? Poll(string iface, double elapsedSeconds)
    {
        var read = reader.Read(iface);
        if (read is null) { Reset(); _lastIface = null; return null; }

        // Смена интерфейса (например, переподключение tun0→tun1) обнуляет базу,
        // чтобы не посчитать фиктивную дельту между разными туннелями.
        if (iface != _lastIface) Reset();
        _lastIface = iface;

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
