using Vantah.Core.Cli;
using Vantah.Core.Errors;
using Vantah.Core.Models;
using Vantah.Core.Parsing;

namespace Vantah.Core.Vpn;

/// <summary>Сбой команды CLI. Текст для пользователя собирается из <see cref="Error"/> в UI.</summary>
public sealed class VpnCommandException(AppError error) : Exception(error.ToString()), IAppErrorException
{
    public AppError Error { get; } = error;
}

public sealed class VpnService(ICliRunner cli) : IVpnService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan QuickTimeout   = TimeSpan.FromSeconds(15);

    public async Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["status"], QuickTimeout, ct);
        // Транзиентный сбой (демон/сеть моргнули) иначе молча парсился бы как «отключено»
        // и рвал бы активную сессию истории — сбой ловим по коду возврата.
        if (!r.Ok)
            throw new VpnCommandException(AppError.FromCli(r, "status"));
        return StatusParser.Parse(r.Stdout);
    }

    public async Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["list-locations", count.ToString()], QuickTimeout, ct);
        if (!r.Ok)
            throw new VpnCommandException(AppError.FromCli(r, "list-locations"));
        return LocationsParser.Parse(r.Stdout);
    }

    public async Task<VpnStatus> ConnectAsync(
        string? location, bool fastest,
        IpVersionPreference ipVersion = IpVersionPreference.Auto,
        CancellationToken ct = default)
    {
        var args = new List<string> { "connect" };
        if (fastest) args.Add("-f");
        else if (!string.IsNullOrWhiteSpace(location)) { args.Add("-l"); args.Add(location); }
        if (ipVersion == IpVersionPreference.IPv4Only) args.Add("-4");
        else if (ipVersion == IpVersionPreference.IPv6Only) args.Add("-6");
        args.Add("-y");

        var r = await cli.RunAsync(args.ToArray(), ConnectTimeout, ct);
        if (!r.Ok)
            throw new VpnCommandException(AppError.FromCli(r, "connect"));
        return await GetStatusAsync(ct);
    }

    public async Task<VpnStatus> DisconnectAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["disconnect"], QuickTimeout, ct);
        if (!r.Ok)
            throw new VpnCommandException(AppError.FromCli(r, "disconnect"));
        return await GetStatusAsync(ct);
    }

    public async Task<License> GetLicenseAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["license"], QuickTimeout, ct);
        // Парсер на нераспарсенном выводе молча вернёт License("", "UNKNOWN", 0, null),
        // поэтому сбой (частый случай — не выполнен вход) ловим по коду возврата.
        if (!r.Ok)
            throw new VpnCommandException(AppError.FromCli(r, "license"));
        return LicenseParser.Parse(r.Stdout);
    }

    public async Task<string?> GetCliVersionAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["--version"], QuickTimeout, ct);
        if (!r.Ok) return null;
        var version = Ansi.Strip(r.Stdout).Trim();
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }
}
