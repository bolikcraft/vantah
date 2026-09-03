using Vantah.Core.Cli;
using Vantah.Core.Errors;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.Parsing;

namespace Vantah.Core.Vpn;

/// <summary>Сбой команды CLI. Текст для пользователя собирается из <see cref="Error"/> в UI.</summary>
public sealed class VpnCommandException(AppError error) : Exception(error.ToString()), IAppErrorException
{
    public AppError Error { get; } = error;
}

public sealed class VpnService(ICliRunner cli, IAppLog? log = null) : IVpnService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan QuickTimeout   = TimeSpan.FromSeconds(15);

    private readonly IAppLog _log = log ?? NullAppLog.Instance;

    // Последняя записанная строка про статус: опрос идёт каждые 4 с, повторы в лог не пишем.
    private string? _lastStatusLine;

    public async Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["status"], QuickTimeout, ct);
        // Транзиентный сбой (демон/сеть моргнули) иначе молча парсился бы как «отключено»
        // и рвал бы активную сессию истории — сбой ловим по коду возврата.
        if (!r.Ok)
            throw new VpnCommandException(AppError.FromCli(r, "status"));
        var status = StatusParser.Parse(r.Stdout);
        LogStatus(r.Stdout, status);
        return status;
    }

    // Сырой ответ CLI рядом с результатом разбора: по этой паре видно, где именно разошлись
    // ответ и показанное состояние.
    private void LogStatus(string stdout, VpnStatus status)
    {
        if (!_log.Enabled)
        {
            // Лог могли выключить и включить снова — тогда первую же строку надо записать.
            _lastStatusLine = null;
            return;
        }

        var line = $"status: \"{FirstLine(stdout)}\" → {Describe(status)}";
        if (line == _lastStatusLine) return;
        _lastStatusLine = line;
        _log.Write(line);
    }

    private static string FirstLine(string stdout)
    {
        foreach (var raw in Ansi.Strip(stdout).Split('\n'))
            if (raw.Trim() is { Length: > 0 } line) return line;
        return "";
    }

    private static string Describe(VpnStatus status) =>
        string.Join(", ", new[]
            {
                status.Phase.ToString(), status.Location, status.Mode,
                status.Interface ?? status.Endpoint,
            }
            .Where(part => !string.IsNullOrEmpty(part)));

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
        bool killSwitch = false,
        CancellationToken ct = default)
    {
        var args = new List<string> { "connect" };
        if (fastest) args.Add("-f");
        else if (!string.IsNullOrWhiteSpace(location)) { args.Add("-l"); args.Add(location); }
        if (ipVersion == IpVersionPreference.IPv4Only) args.Add("-4");
        else if (ipVersion == IpVersionPreference.IPv6Only) args.Add("-6");
        // Kill switch: демон бесконечно переподключается при обрыве. disconnect его гасит.
        if (killSwitch) args.Add("--boot");
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
