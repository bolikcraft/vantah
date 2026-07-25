namespace Vantah.Core.Models;

public sealed record VpnStatus(bool IsConnected, string? Location, string? Mode, string? Interface)
{
    /// <summary>CLI ответил «VPN is starting»: туннель ещё поднимается (флаг --boot у kill switch).
    /// Отдельный флаг, а не позиционный параметр: конструктор используют десятки вызовов.</summary>
    public bool IsStarting { get; init; }

    public static readonly VpnStatus Disconnected = new(false, null, null, null);
    public static readonly VpnStatus Starting = new(false, null, null, null) { IsStarting = true };
}
