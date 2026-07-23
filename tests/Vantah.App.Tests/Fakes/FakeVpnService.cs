using Vantah.Core.Models;
using Vantah.Core.Vpn;

namespace Vantah.App.Tests.Fakes;

public sealed class FakeVpnService : IVpnService
{
    public List<(string? Location, bool Fastest, IpVersionPreference Ip, bool KillSwitch)> Connects { get; } = [];

    public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
        IpVersionPreference ipVersion = IpVersionPreference.Auto,
        bool killSwitch = false, CancellationToken ct = default)
    {
        Connects.Add((location, fastest, ipVersion, killSwitch));
        return Task.FromResult(VpnStatus.Disconnected);
    }

    public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(VpnStatus.Disconnected);
    public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Location>>([]);
    public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
        Task.FromResult(VpnStatus.Disconnected);
    public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
        Task.FromResult(new License("", "", 0, null));
    public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>("test");
}
