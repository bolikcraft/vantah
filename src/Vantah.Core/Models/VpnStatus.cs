namespace Vantah.Core.Models;

/// <summary>Фаза туннеля по ответу `adguardvpn-cli status`.</summary>
public enum VpnPhase
{
    Disconnected,

    /// <summary>«VPN is starting»: туннель ещё поднимается.</summary>
    Starting,

    /// <summary>Связь оборвалась, и kill switch (`connect --boot`) бесконечно ретраит:
    /// «Connection lost. Waiting to reconnect to …», «Reconnecting to …» либо «Network lost.
    /// Waiting to connect to …» (пропала сама сеть — CLI ждёт её возврата).
    /// Локация и режим в этих формах известны, туннеля при этом нет.</summary>
    Reconnecting,

    Connected,
}

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

    /// <summary>Источник истины о состоянии: <see cref="IsConnected"/> различает только два
    /// исхода. Отдельное свойство, а не позиционный параметр: конструктор используют десятки
    /// вызовов.</summary>
    public VpnPhase Phase { get; init; } = IsConnected ? VpnPhase.Connected : VpnPhase.Disconnected;

    public static readonly VpnStatus Disconnected = new(false, null, null, null);
    public static readonly VpnStatus Starting = new(false, null, null, null) { Phase = VpnPhase.Starting };

    /// <summary>Kill switch восстанавливает связь. Локацию и режим сохраняем, чтобы UI не терял
    /// подпись, пока идут ретраи.</summary>
    public static VpnStatus Reconnecting(string? location, string? mode,
        string? iface = null, string? endpoint = null) =>
        new(false, location, mode, iface) { Phase = VpnPhase.Reconnecting, Endpoint = endpoint };
}
