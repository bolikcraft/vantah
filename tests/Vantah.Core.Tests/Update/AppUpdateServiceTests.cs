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
            store.Update(s => s with { Enabled = false });
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
    public async Task Check_reports_nothing_when_the_toggle_was_switched_off_while_it_was_running()
    {
        // Иначе плашка всплыла бы сразу после того, как пользователь выключил проверку.
        var store = new AppUpdateStore(TempPath());
        var service = new AppUpdateService(new SourceThatEditsState(store, Newer), store, "0.1.0");

        Assert.Null(await service.CheckAsync(Now));
    }

    [Fact]
    public async Task Release_found_while_the_toggle_was_switched_off_is_still_stored()
    {
        // Запрос уже состоялся, и его ответ верен: включат проверку снова — сеть не понадобится.
        var store = new AppUpdateStore(TempPath());
        var service = new AppUpdateService(new SourceThatEditsState(store, Newer), store, "0.1.0");

        await service.CheckAsync(Now);

        Assert.Equal(Newer, store.Load().LatestRelease);
        Assert.Equal(Now, store.Load().LastCheckUtc);
    }

    [Fact]
    public async Task Failed_check_reports_nothing_when_the_toggle_was_switched_off_while_it_was_running()
    {
        // Сеть молчит, а релиз со вчерашней проверки лежит в файле — его тоже не показываем.
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);

        var store = new AppUpdateStore(path);
        var service = new AppUpdateService(new SourceThatEditsState(store, release: null), store, "0.1.0");

        Assert.Null(await service.CheckAsync(Now.AddHours(25)));
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
    public async Task Successful_check_stores_the_release()
    {
        var (service, store, _) = Build(Newer);

        await service.CheckAsync(Now);

        Assert.Equal(Newer, store.Load().LatestRelease);
    }

    [Fact]
    public async Task Second_check_within_24h_returns_the_stored_release_without_hitting_the_source()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);

        var (second, _, source) = Build(Newer, path: path);
        var info = await second.CheckAsync(Now.AddHours(23));

        Assert.Equal(Newer, info);
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Release_is_remembered_across_restarts()
    {
        // Плашку увидели, вышли через «Выйти», запустили снова в пределах суток — и без сети.
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);

        var (second, _, _) = Build(release: null, path: path);

        Assert.Equal(Newer, await second.CheckAsync(Now.AddHours(1)));
    }

    [Fact]
    public async Task Failed_check_after_24h_falls_back_to_the_stored_release()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);

        var (second, store, source) = Build(release: null, path: path);
        var info = await second.CheckAsync(Now.AddHours(25));

        Assert.Equal(Newer, info);
        Assert.Equal(1, source.Calls);
        Assert.Equal(Now, store.Load().LastCheckUtc);
    }

    [Fact]
    public async Task Check_after_24h_replaces_the_stored_release()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);

        var next = new AppUpdateInfo("v0.3.0", "https://github.com/bolikcraft/vantah/releases/tag/v0.3.0");
        var (second, store, _) = Build(next, path: path);
        var info = await second.CheckAsync(Now.AddHours(25));

        Assert.Equal(next, info);
        Assert.Equal(next, store.Load().LatestRelease);
    }

    [Fact]
    public async Task Stored_release_is_not_an_update_once_the_app_is_upgraded_to_it()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);

        var (upgraded, _, _) = Build(Newer, current: "0.2.0", path: path);

        Assert.Null(await upgraded.CheckAsync(Now.AddHours(1)));
    }

    [Fact]
    public async Task Disabled_check_hides_the_stored_release()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);
        first.Enabled = false;

        var (second, _, _) = Build(Newer, path: path);

        Assert.Null(await second.CheckAsync(Now.AddHours(1)));
    }

    [Fact]
    public async Task State_from_an_older_version_without_a_release_is_checked_before_24h()
    {
        // Прежние версии писали время проверки, но не релиз. Иначе новая версия сутки не знала бы
        // о релизе, который старая уже нашла.
        var (service, store, source) = Build(Newer);
        store.Update(s => s with { LastCheckUtc = Now.AddHours(-1) });

        var info = await service.CheckAsync(Now);

        Assert.Equal(Newer, info);
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task Once_the_release_is_stored_the_24h_cooldown_holds_again()
    {
        var (service, store, source) = Build(Newer);
        store.Update(s => s with { LastCheckUtc = Now.AddHours(-1) });
        await service.CheckAsync(Now);

        await service.CheckAsync(Now.AddHours(1));

        Assert.Equal(1, source.Calls);
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
    public async Task Dismissed_version_is_still_reported_by_the_check()
    {
        // Крестик прячет только плашку: «О программе» о новой версии говорит и после него.
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);
        first.Dismiss("v0.2.0");

        var (second, _, _) = Build(Newer, path: path);

        Assert.Equal(Newer, await second.CheckAsync(Now.AddHours(25)));
    }

    [Fact]
    public async Task Dismissed_version_stays_dismissed_on_later_checks()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);
        first.Dismiss("v0.2.0");

        var (second, _, _) = Build(Newer, path: path);
        var info = await second.CheckAsync(Now.AddHours(25));

        Assert.NotNull(info);
        Assert.True(second.IsDismissed(info));
    }

    [Fact]
    public void Nothing_is_dismissed_until_the_cross_is_clicked()
    {
        var (service, _, _) = Build(Newer);

        Assert.False(service.IsDismissed(Newer));
    }

    [Fact]
    public async Task A_version_newer_than_the_dismissed_one_is_not_dismissed()
    {
        var path = TempPath();
        var (first, _, _) = Build(Newer, path: path);
        await first.CheckAsync(Now);
        first.Dismiss("v0.2.0");

        var next = new AppUpdateInfo("v0.3.0", "https://github.com/bolikcraft/vantah/releases/tag/v0.3.0");
        var (second, _, _) = Build(next, path: path);
        var info = await second.CheckAsync(Now.AddHours(25));

        Assert.Equal("v0.3.0", info?.Version);
        Assert.False(second.IsDismissed(info!));
    }
}
