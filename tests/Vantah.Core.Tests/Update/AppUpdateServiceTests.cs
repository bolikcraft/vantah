using Vantah.Core.Tests.Fakes;
using Vantah.Core.Update;
using Xunit;

public class AppUpdateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly AppUpdateInfo Newer =
        new("v0.2.0", "https://github.com/bolikcraft/vantah/releases/tag/v0.2.0");

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "appupdate.json");

    private static (AppUpdateService Service, AppUpdateStore Store, FakeAppReleaseSource Source) Build(
        AppUpdateInfo? release, string current = "0.1.0", string? path = null)
    {
        var store = new AppUpdateStore(path ?? TempPath());
        var source = new FakeAppReleaseSource(release);
        return (new AppUpdateService(source, store, current), store, source);
    }

    /// <summary>Источник, который во время запроса меняет состояние — как настройки на UI-потоке.</summary>
    private sealed class SourceThatEditsState(AppUpdateStore store, AppUpdateInfo? release) : IAppReleaseSource
    {
        public Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default)
        {
            store.Save(store.Load() with { Enabled = false });
            return Task.FromResult(release);
        }
    }

    [Fact]
    public async Task Check_does_not_revive_a_toggle_switched_off_while_it_was_running()
    {
        // Запрос длится до 10 секунд: за это время пользователь может снять галку в настройках,
        // и запись снимка состояния, снятого ДО запроса, вернула бы её обратно.
        var store = new AppUpdateStore(TempPath());
        var service = new AppUpdateService(new SourceThatEditsState(store, Newer), store, "0.1.0");

        await service.CheckAsync(Now);

        Assert.False(store.Load().Enabled);
    }

    [Fact]
    public async Task First_check_reports_a_newer_release()
    {
        var (service, _, source) = Build(Newer);

        var info = await service.CheckAsync(Now);

        Assert.Equal("v0.2.0", info?.Version);
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task Successful_check_records_its_time()
    {
        var (service, store, _) = Build(Newer);

        await service.CheckAsync(Now);

        Assert.Equal(Now, store.Load().LastCheckUtc);
    }

    [Fact]
    public async Task Second_check_within_24h_does_not_hit_the_source()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);

        var (second, _, source) = Build(Newer, path: path);
        var info = await second.CheckAsync(Now.AddHours(23));

        Assert.Null(info);
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Check_after_24h_hits_the_source_again()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);

        var (second, _, source) = Build(Newer, path: path);
        var info = await second.CheckAsync(Now.AddHours(25));

        Assert.Equal("v0.2.0", info?.Version);
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task Disabled_check_never_touches_the_network()
    {
        var (service, store, source) = Build(Newer);
        service.Enabled = false;

        var info = await service.CheckAsync(Now);

        Assert.Null(info);
        Assert.Equal(0, source.Calls);
        Assert.False(store.Load().Enabled);
    }

    [Fact]
    public async Task Failed_check_does_not_start_the_24h_cooldown()
    {
        var (service, store, _) = Build(release: null);

        var info = await service.CheckAsync(Now);

        Assert.Null(info);
        Assert.Null(store.Load().LastCheckUtc);
    }

    [Fact]
    public async Task Same_version_is_not_an_update()
    {
        var (service, _, _) = Build(Newer, current: "0.2.0");

        Assert.Null(await service.CheckAsync(Now));
    }

    [Fact]
    public async Task Older_release_is_not_an_update()
    {
        var (service, _, _) = Build(Newer, current: "0.3.0");

        Assert.Null(await service.CheckAsync(Now));
    }

    [Fact]
    public async Task Unparsable_tag_is_not_an_update()
    {
        var (service, _, _) = Build(new AppUpdateInfo("latest", "https://example.invalid"));

        Assert.Null(await service.CheckAsync(Now));
    }

    [Fact]
    public async Task Dismissed_version_stays_hidden_on_later_checks()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);
        first.Dismiss("v0.2.0");

        var (second, _, _) = Build(Newer, path: path);
        Assert.Null(await second.CheckAsync(Now.AddHours(25)));
    }

    [Fact]
    public async Task A_version_newer_than_the_dismissed_one_is_shown()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);
        first.Dismiss("v0.2.0");

        var next = new AppUpdateInfo("v0.3.0", "https://github.com/bolikcraft/vantah/releases/tag/v0.3.0");
        var (second, _, _) = Build(next, path: path);

        Assert.Equal("v0.3.0", (await second.CheckAsync(Now.AddHours(25)))?.Version);
    }
}
