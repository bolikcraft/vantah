namespace Vantah.Core.Models;

public sealed record VpnStatus(bool IsConnected, string? Location, string? Mode, string? Interface)
{
    /// <summary>Адрес прокси в режиме SOCKS («127.0.0.1:1080»); в режиме туннеля — null.
    /// Туннельного интерфейса в SOCKS нет, и трафик считается по этому адресу.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Порт прокси из <see cref="Endpoint"/>; null, если это не SOCKS.</summary>
    public int? SocksPort =>
        Endpoint?.LastIndexOf(':') is > 0 and var i
        && int.TryParse(Endpoint.AsSpan(i + 1), out var port)
            ? port
            : null;

    /// <summary>CLI ответил «VPN is starting»: туннель ещё поднимается (флаг --boot у kill switch).
    /// Отдельный флаг, а не позиционный параметр: конструктор используют десятки вызовов.</summary>
    public bool IsStarting { get; init; }

    public static readonly VpnStatus Disconnected = new(false, null, null, null);
    public static readonly VpnStatus Starting = new(false, null, null, null) { IsStarting = true };
}
