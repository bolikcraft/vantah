namespace Vantah.Core.Traffic;

public sealed class SysfsTrafficReader(string root = "/sys/class/net") : ITrafficReader
{
    public (long rx, long tx)? Read(string iface)
    {
        var dir = Path.Combine(root, iface, "statistics");
        if (!Directory.Exists(dir)) return null;
        try
        {
            var rx = long.Parse(File.ReadAllText(Path.Combine(dir, "rx_bytes")).Trim());
            var tx = long.Parse(File.ReadAllText(Path.Combine(dir, "tx_bytes")).Trim());
            return (rx, tx);
        }
        catch { return null; }
    }
}
