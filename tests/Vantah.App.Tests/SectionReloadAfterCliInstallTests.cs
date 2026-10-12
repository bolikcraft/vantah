using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
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
/// Экран аккаунта и «О программе» читают CLI один раз при старте. Без CLI там оставалось
/// «нужен AdGuard VPN CLI» и «unavailable» даже после установки — до перезапуска приложения.
/// </summary>
public class SectionReloadAfterCliInstallTests
{
    /// <summary>VPN-сервис, у которого CLI «ставят» флагом.</summary>
    private sealed class InstallableVpn : IVpnService
    {
        public bool Installed { get; set; }

        private Task<T> Answer<T>(T value) => Installed
            ? Task.FromResult(value)
            : Task.FromException<T>(new CliMissingException(
                new AppError(AppErrorCode.CliMissing, "adguardvpn-cli"), new System.ComponentModel.Win32Exception(2)));

        public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
            Answer(new License("user@example.com", "AdGuard VPN Unlimited", 10, "2027-01-31"));
        public Task<string?> GetCliVersionAsync(CancellationToken ct = default) => Answer<string?>("1.5.10");
        public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) => Answer(VpnStatus.Disconnected);
        public Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default) =>
            Answer<IReadOnlyList<Location>>([]);
        public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
            IpVersionPreference ipVersion = IpVersionPreference.Auto,
            bool killSwitch = false, CancellationToken ct = default) => Answer(VpnStatus.Disconnected);
        public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) => Answer(VpnStatus.Disconnected);
    }

    [AvaloniaFact]
    public async Task Account_and_about_reload_once_the_cli_appears()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var vpn = new InstallableVpn();
        var store = new AppStateStore();
        var coord = new VpnCoordinator(
            vpn, new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
                new ActiveSessionStore(Path.Combine(dir, "connection-active"))),
            new IpVersionStore(Path.Combine(dir, "ip-version")), new FakeAuthService());
        var account = new LicenseViewModel(vpn, new FakeAuthService(), coord);
        var about = new AboutViewModel(vpn);
        var reloader = new SectionReloader(store, [account, about]);

        await Task.WhenAll(account.LoadTask, about.LoadTask);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Localizer.Instance[LocKeys.Error_CliMissing], account.Error);
        Assert.Equal(Localizer.Instance[LocKeys.Common_NotAvailable], about.CliVersion);

        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionState.CliMissing, store.Current.Connection);
        vpn.Installed = true;
        await coord.PollOnceAsync(TestContext.Current.CancellationToken);
        await reloader.LastRunTask;
        Dispatcher.UIThread.RunJobs();

        Assert.Null(account.Error);
        Assert.Equal("user@example.com", account.Email);
        Assert.Equal("1.5.10", about.CliVersion);
    }
}
