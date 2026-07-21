using Vantah.Core.Cli;
using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Vantah.Core.Tests.Fakes;
using Vantah.Core.Vpn; // VpnCommandException
using Xunit;

public class ExclusionsServiceTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), $"vantah-esvc-{Guid.NewGuid():N}");

    [Fact]
    public async Task Get_runs_show_and_parses()
    {
        var cli = new FakeCliRunner().Enqueue("Exclusions for SELECTIVE mode:\nbank.example\n");
        var svc = new ExclusionsService(cli, new ExclusionsStore(TempDir()));

        var snap = await svc.GetAsync();

        Assert.Equal(new[] { "site-exclusions", "show" }, cli.Calls[0]);
        Assert.Equal(SiteExclusionMode.Selective, snap.Mode);
        Assert.Equal("bank.example", Assert.Single(snap.Domains));
    }

    [Fact]
    public async Task Add_passes_domain()
    {
        var cli = new FakeCliRunner();
        var svc = new ExclusionsService(cli, new ExclusionsStore(TempDir()));
        await svc.AddAsync("example.com");
        Assert.Equal(new[] { "site-exclusions", "add", "--", "example.com" }, cli.Calls[0]);
    }

    [Fact]
    public async Task Remove_passes_domain()
    {
        var cli = new FakeCliRunner();
        var svc = new ExclusionsService(cli, new ExclusionsStore(TempDir()));
        await svc.RemoveAsync("example.com");
        Assert.Equal(new[] { "site-exclusions", "remove", "--", "example.com" }, cli.Calls[0]);
    }

    [Fact]
    public async Task Add_failure_throws_with_stderr()
    {
        var cli = new FakeCliRunner().Enqueue(new CliResult(1, "", "invalid domain"));
        var svc = new ExclusionsService(cli, new ExclusionsStore(TempDir()));
        var ex = await Assert.ThrowsAsync<VpnCommandException>(() => svc.AddAsync("nope"));
        Assert.Contains("invalid domain", ex.Message);
    }

    [Fact]
    public async Task SetMode_saves_prev_switches_and_reapplies_target_from_file()
    {
        var dir = TempDir();
        try
        {
            var store = new ExclusionsStore(dir);
            // У целевого (selective) режима заранее сохранён свой список.
            store.Save(SiteExclusionMode.Selective, new[] { "bank.example", "gov.example" });

            var cli = new FakeCliRunner();
            var svc = new ExclusionsService(cli, store);

            // Переключаемся с general (текущие домены general в UI: a.com,b.com) на selective.
            await svc.SetModeAsync(
                from: SiteExclusionMode.General,
                to: SiteExclusionMode.Selective,
                currentDomains: new[] { "a.com", "b.com" });

            // 1) домены прежнего режима сохранены в general.txt
            Assert.Equal(new[] { "a.com", "b.com" }, store.Load(SiteExclusionMode.General));

            // 2) вызван переключатель режима
            Assert.Equal(new[] { "site-exclusions", "mode", "selective" }, cli.Calls[0]);

            // 3) переприменены домены целевого режима из файла
            Assert.Equal(new[] { "site-exclusions", "add", "--", "bank.example" }, cli.Calls[1]);
            Assert.Equal(new[] { "site-exclusions", "add", "--", "gov.example" }, cli.Calls[2]);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
