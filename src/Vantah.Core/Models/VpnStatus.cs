namespace Vantah.Core.Models;

public sealed record VpnStatus(bool IsConnected, string? Location, string? Mode, string? Interface)
{
    public static readonly VpnStatus Disconnected = new(false, null, null, null);
}
