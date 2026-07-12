using Vantah.Core.Cli;
using Vantah.Core.Models;
using Vantah.Core.Parsing;

namespace Vantah.Core.Vpn;

public sealed class VpnCommandException(string message) : Exception(message);

public sealed class VpnService(ICliRunner cli) : IVpnService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan QuickTimeout   = TimeSpan.FromSeconds(15);

    public async Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["status"], QuickTimeout, ct);
        return StatusParser.Parse(r.Stdout);
    }

    public async Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["list-locations", count.ToString()], QuickTimeout, ct);
        return LocationsParser.Parse(r.Stdout);
    }

    public async Task<VpnStatus> ConnectAsync(string? location, bool fastest, CancellationToken ct = default)
    {
        var args = new List<string> { "connect" };
        if (fastest) args.Add("-f");
        else if (!string.IsNullOrWhiteSpace(location)) { args.Add("-l"); args.Add(location); }
        args.Add("-y");

        var r = await cli.RunAsync(args.ToArray(), ConnectTimeout, ct);
        if (!r.Ok)
            throw new VpnCommandException(FirstNonEmpty(r.Stderr, r.Stdout, "connect завершился с ошибкой"));
        return await GetStatusAsync(ct);
    }

    public async Task<VpnStatus> DisconnectAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["disconnect"], QuickTimeout, ct);
        if (!r.Ok)
            throw new VpnCommandException(FirstNonEmpty(r.Stderr, r.Stdout, "disconnect завершился с ошибкой"));
        return await GetStatusAsync(ct);
    }

    public async Task<License> GetLicenseAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["license"], QuickTimeout, ct);
        return LicenseParser.Parse(r.Stdout);
    }

    private static string FirstNonEmpty(params string[] xs) =>
        xs.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
}
