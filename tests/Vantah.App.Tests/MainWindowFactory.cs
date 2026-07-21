using System;
using System.IO;
using System.Threading.Tasks;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Auth;
using Vantah.Core.Exclusions;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;

namespace Vantah.App.Tests;

/// <summary>Сборка настоящего MainWindow на фейках и временных путях — общая для UI-тестов.</summary>
public static class MainWindowFactory
{
    public static MainWindow Build(
        LoginState state = LoginState.LoggedIn,
        AppStateStore? store = null,
        FakeAuthService? auth = null,
        UpdateBannerViewModel? banner = null)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        store ??= new AppStateStore();
        store.Set(s => s with { LoginState = state });

        var runner = new FakeCliRunner();
        var vpn = new VpnService(runner);
        var ipVersionStore = new IpVersionStore(Path.Combine(temp, "ip-version"));
        auth ??= new FakeAuthService { State = state };
        var coordinator = new VpnCoordinator(
            vpn, new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(temp, "history")),
                new ActiveSessionStore(Path.Combine(temp, "connection-active"))),
            ipVersionStore, auth);
        var exclusionsStore = new ExclusionsStore(Path.Combine(temp, "site-exclusions"));

        var vm = new MainWindowViewModel(
            new StatusViewModel(coordinator, store, new VpnLogReader(Path.Combine(temp, "vpn.log")),
                new HistoryViewModel(coordinator, store), ipVersionStore),
            new LocationsViewModel(vpn, coordinator, new FavoritesStore(Path.Combine(temp, "favorites.json")), store),
            new DomainsViewModel(new ExclusionsService(runner, exclusionsStore), exclusionsStore, store),
            new LicenseViewModel(vpn), new AboutViewModel(vpn), new ProcessesViewModel(new StubMonitor()),
            new ConfigViewModel(new FakeConfigService(), store, new LanguageStore(Path.Combine(temp, "language")),
                new FakeUpdateChecker(), new FakeLogExporter(), () => Task.FromResult<string?>(null)),
            new LoginViewModel(auth, coordinator), auth, coordinator, store,
            banner);

        return new MainWindow { DataContext = vm };
    }
}
