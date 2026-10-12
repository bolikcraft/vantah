using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.Core.Cli;
using Vantah.Core.Errors;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

namespace Vantah.App.Tests;

/// <summary>
/// Без adguardvpn-cli экран «Статус» показывал «Ошибка» и сырой текст исключения .NET.
/// Теперь у этого случая своё состояние. Раннер настоящий: CLI «ставим» симлинком на true.
/// </summary>
public class VpnCoordinatorCliMissingTests
{
    [Fact]
    public async Task First_poll_without_cli_shows_cli_missing_at_once()
    {
        var (coord, store, _, _) = Make();

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        // Порог в два промаха — для зависшего `status`; отсутствие файла ждать незачем.
        Assert.Equal(ConnectionState.CliMissing, store.Current.Connection);
        Assert.Equal(AppErrorCode.CliMissing, store.Current.Error?.Code);
    }

    [Fact]
    public async Task Poll_returns_to_normal_state_once_cli_is_installed()
    {
        var (coord, store, cliPath, _) = Make();
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionState.CliMissing, store.Current.Connection);

        File.CreateSymbolicLink(cliPath, TrueBin());
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Disconnected, store.Current.Connection);
        Assert.Null(store.Current.Error);
    }

    /// <summary>
    /// Промах до пропажи CLI не должен складываться с промахом после установки: порог
    /// «два подряд» после CliMissing отсчитывается с нуля.
    /// </summary>
    [Fact]
    public async Task Failure_threshold_starts_over_after_cli_missing()
    {
        var (coord, store, cliPath, _) = Make();
        var falseBin = new[] { "/bin/false", "/usr/bin/false" }.First(File.Exists);

        File.CreateSymbolicLink(cliPath, falseBin);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);   // промах 1
        File.Delete(cliPath);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);   // CLI нет
        Assert.Equal(ConnectionState.CliMissing, store.Current.Connection);

        File.CreateSymbolicLink(cliPath, falseBin);
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionState.CliMissing, store.Current.Connection);   // первый промах глотаем

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionState.Error, store.Current.Connection);
    }

    [Fact]
    public async Task Connect_without_cli_shows_cli_missing_instead_of_error()
    {
        var (coord, store, _, _) = Make();

        await coord.ConnectAsync("Amsterdam", fastest: false, TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.CliMissing, store.Current.Connection);
        Assert.Equal(AppErrorCode.CliMissing, store.Current.Error?.Code);
    }

    /// <summary>Отсутствие CLI — не обрыв связи: плашку «связь потеряна» оно давать не должно.</summary>
    [Fact]
    public async Task Missing_cli_is_not_reported_as_a_link_drop()
    {
        var (coord, _, _, link) = Make();

        for (var i = 0; i < 3; i++)
            await coord.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Empty(link.Events);
    }

    private static string TrueBin() =>
        new[] { "/bin/true", "/usr/bin/true" }.First(File.Exists);

    private static (VpnCoordinator Coordinator, AppStateStore Store, string CliPath, RecordingLinkObserver Link) Make()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cliPath = Path.Combine(dir, "adguardvpn-cli");

        var store = new AppStateStore();
        var link = new RecordingLinkObserver();
        var coord = new VpnCoordinator(
            new VpnService(new CliRunner(cliPath)), new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
                new ActiveSessionStore(Path.Combine(dir, "connection-active"))),
            new IpVersionStore(Path.Combine(dir, "ip-version")), new FakeAuthService(),
            new LastLocationStore(Path.Combine(dir, "last-location")), link: link);
        return (coord, store, cliPath, link);
    }
}
