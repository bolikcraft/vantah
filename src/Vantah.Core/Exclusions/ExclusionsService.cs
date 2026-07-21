using Vantah.Core.Cli;
using Vantah.Core.Models;
using Vantah.Core.Vpn; // VpnCommandException

namespace Vantah.Core.Exclusions;

public sealed class ExclusionsService(ICliRunner cli, ExclusionsStore store) : IExclusionsService
{
    private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(15);

    public async Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["site-exclusions", "show"], QuickTimeout, ct);
        if (!r.Ok) throw new VpnCommandException(FirstNonEmpty(r.Stderr, r.Stdout, "site-exclusions show завершился с ошибкой"));
        return ExclusionsParser.Parse(r.Stdout);
    }

    public async Task AddAsync(string domain, CancellationToken ct = default)
    {
        // `--` — терминатор опций: домен всегда трактуется как позиционный аргумент,
        // даже если строка начинается с «-» (проверено на adguardvpn-cli v1.7.13).
        var r = await cli.RunAsync(["site-exclusions", "add", "--", domain], QuickTimeout, ct);
        if (!r.Ok) throw new VpnCommandException(FirstNonEmpty(r.Stderr, r.Stdout, $"не удалось добавить {domain}"));
    }

    public async Task RemoveAsync(string domain, CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["site-exclusions", "remove", "--", domain], QuickTimeout, ct);
        if (!r.Ok) throw new VpnCommandException(FirstNonEmpty(r.Stderr, r.Stdout, $"не удалось удалить {domain}"));
    }

    public async Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
        IReadOnlyList<string> currentDomains, CancellationToken ct = default)
    {
        // 1) сохранить домены прежнего режима в его файл
        store.Save(from, currentDomains);

        // 2) переключить режим в CLI
        var r = await cli.RunAsync(["site-exclusions", "mode", to.ToCliArg()], QuickTimeout, ct);
        if (!r.Ok) throw new VpnCommandException(FirstNonEmpty(r.Stderr, r.Stdout, $"не удалось переключить режим на {to.ToCliArg()}"));

        // 3) переприменить домены целевого режима из его файла.
        // NB: цикл НЕ атомарен — если add упадёт на середине, CLI останется в целевом
        // режиме с частичным списком; восстановимо, т.к. все домены сохранены в файле.
        foreach (var domain in store.Load(to))
            await AddAsync(domain, ct);
    }

    private static string FirstNonEmpty(params string[] xs) =>
        xs.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
}
