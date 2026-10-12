using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Cli;
using Vantah.Core.Errors;
using Vantah.Core.Exclusions;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Localization;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;

namespace Vantah.App.Tests.Localization;

/// <summary>
/// Слова, по которым тест каталога AppImageHub бракует снимок окна (его распознаёт OCR).
/// Каталог запускает AppImage без adguardvpn-cli, поэтому экран «нужен CLI» обязан их обходить.
/// </summary>
internal static class AppImageHubOcr
{
    /// <summary>Снимок с таким словом валит тест.</summary>
    public static readonly Regex Error = new(
        "traceback|exception|segmentation fault|fatal|error while loading|glibc|not installed"
        + "|cannot open display|permission denied|no such file|could not (load|find|open|start|initiali)"
        + "|failed to (load|start|open|initiali|create)|cannot (load|find|open|execute|configure)"
        + "|unable to (load|find|open|start)|command not found|core dumped",
        RegexOptions.IgnoreCase);

    /// <summary>Тест проходит, но без метки screenshot-ok.</summary>
    public static readonly Regex Warning = new(
        "error|failed|failure|could not|cannot|unable to|not found", RegexOptions.IgnoreCase);

    public static bool Flags(string text) => Error.IsMatch(text) || Warning.IsMatch(text);
}

public class CliMissingTextTests
{
    private static readonly string[] NewKeys =
    [
        LocKeys.Status_CliMissing,
        LocKeys.Status_CliMissingHint,
        LocKeys.Status_CliMissingInstall,
        LocKeys.Error_CliMissing,
    ];

    /// <summary>Страховка от правки вроде «AdGuard VPN CLI not found»: такой текст снова завалит каталог.</summary>
    [Fact]
    public void English_texts_avoid_the_words_the_catalog_rejects()
    {
        var loc = new Localizer();

        foreach (var key in NewKeys)
        {
            var text = loc[key];
            Assert.NotEqual(key, text);
            Assert.False(AppImageHubOcr.Error.IsMatch(text), $"{key}: «{text}» валит тест каталога");
            Assert.False(AppImageHubOcr.Warning.IsMatch(text), $"{key}: «{text}» снимает метку screenshot-ok");
        }
    }

    /// <summary>Сама проверка не пустышка: слова из старого экрана она ловит.</summary>
    [Theory]
    [InlineData("Error")]
    [InlineData("No such file or directory")]
    [InlineData("AdGuard VPN CLI not found")]
    public void Guard_catches_the_old_texts(string text) => Assert.True(AppImageHubOcr.Flags(text));

    [Theory]
    [InlineData("en", "AdGuard VPN CLI is needed to do this.")]
    [InlineData("ru", "Для этого нужен AdGuard VPN CLI.")]
    public void Cli_missing_error_has_its_own_text(string lang, string expected)
    {
        var loc = new Localizer();
        loc.SetLanguage(lang);

        Assert.Equal(expected, loc[LocKeys.Error_CliMissing]);
    }

    /// <summary>
    /// Остальные разделы показывают свою строку вместо текста исключения .NET. Цепочка
    /// настоящая: раннер с несуществующим файлом → сервис → вьюмодель.
    /// </summary>
    [AvaloniaFact]
    public async Task Sections_show_the_cli_missing_text_instead_of_the_exception()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var runner = new CliRunner(Path.Combine(dir, "adguardvpn-cli"));
        var vpn = new VpnService(runner);
        var store = new AppStateStore();
        var coord = new VpnCoordinator(
            vpn, new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
                new ActiveSessionStore(Path.Combine(dir, "connection-active"))),
            new IpVersionStore(Path.Combine(dir, "ip-version")), new FakeAuthService());
        var exclusionsStore = new ExclusionsStore(Path.Combine(dir, "site-exclusions"));

        var locations = new LocationsViewModel(vpn, coord, new FavoritesStore(Path.Combine(dir, "favorites")), store);
        var domains = new DomainsViewModel(new ExclusionsService(runner, exclusionsStore), exclusionsStore, store);
        var settings = new ConfigViewModel(
            new ConfigService(runner), store, new LanguageStore(Path.Combine(dir, "language")),
            new FakeUpdateChecker(), new FakeLogExporter(), () => Task.FromResult<string?>(null),
            new AutoConnectStore(Path.Combine(dir, "autoconnect")));
        var account = new LicenseViewModel(vpn, new FakeAuthService(), coord);

        await Task.WhenAll(locations.LoadTask, domains.LoadTask, settings.LoadTask, account.LoadTask);
        Dispatcher.UIThread.RunJobs();

        var expected = Localizer.Instance[LocKeys.Error_CliMissing];
        Assert.Equal(expected, locations.LoadError);
        Assert.Equal(expected, domains.LoadError);
        Assert.Equal(expected, settings.LoadError);
        Assert.Equal(expected, account.Error);
        Assert.Equal(AppErrorCode.CliMissing, store.Current.Error?.Code);
    }
}
