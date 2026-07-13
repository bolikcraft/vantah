using Vantah.Core.Traffic;

namespace Vantah.App.Tests.Fakes;

/// <summary>Счётчики интерфейса-обманки: интерфейса нет, трафика нет.</summary>
public sealed class FakeTrafficReader : ITrafficReader
{
    public (long rx, long tx)? Read(string iface) => null;
}
