using Vantah.Core.Models;

namespace Vantah.Core.Vpn;

public interface IVpnService
{
    Task<VpnStatus> GetStatusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default);
    Task<VpnStatus> ConnectAsync(
        string? location, bool fastest,
        IpVersionPreference ipVersion = IpVersionPreference.Auto,
        CancellationToken ct = default);
    Task<VpnStatus> DisconnectAsync(CancellationToken ct = default);
    Task<License> GetLicenseAsync(CancellationToken ct = default);
    /// <summary>Версия adguardvpn-cli или null, если получить её не удалось.</summary>
    Task<string?> GetCliVersionAsync(CancellationToken ct = default);
}
