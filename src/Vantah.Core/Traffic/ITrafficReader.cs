namespace Vantah.Core.Traffic;

public interface ITrafficReader
{
    (long rx, long tx)? Read(string iface);   // null, если интерфейса нет
}
