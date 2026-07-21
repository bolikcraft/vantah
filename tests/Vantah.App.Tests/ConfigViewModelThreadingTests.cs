using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Autostart;
using Vantah.Core.Models;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Vantah.Core.Vpn;

/// <summary>
/// Загрузка вкладки «Настройки»: ConfigViewModel.LoadAsync применяет форму (Apply) ПОСЛЕ
/// await IConfigService.GetAsync(). В реальном запуске CLI-обёртка возвращает управление
/// на потоке пула, поэтому Apply маршалим на UI-поток — здесь проверяем наблюдаемый
/// контракт загрузки (форма наполняется без исключения), а не саму потокобезопасность:
/// headless-продолжение всегда возвращается на UI-поток.
/// </summary>
public class ConfigViewModelThreadingTests
{
    // GetAsync, отвечающий АСИНХРОННО (через Task.Yield) — как ScriptedVpn в LocationsViewTests.
    // Прочие Set*-методы форме не нужны здесь — просто возвращают тот же конфиг.
    private sealed class ScriptedConfigService(VpnConfig config) : IConfigService
    {
        public async Task<VpnConfig> GetAsync(CancellationToken ct = default)
        {
            await Task.Yield();
            return config;
        }

        public Task<VpnConfig> SetModeAsync(VpnMode mode, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetSocksPortAsync(int port, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetSocksHostAsync(string host, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetSocksUsernameAsync(string username, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetSocksPasswordAsync(string password, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> ClearSocksAuthAsync(CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetDnsAsync(string upstream, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> ResetDnsAsync(CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetChangeSystemDnsAsync(bool on, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetTunRoutingModeAsync(TunnelRoutingMode mode, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetProtocolAsync(VpnProtocol protocol, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetPostQuantumAsync(bool on, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetUpdateChannelAsync(UpdateChannel channel, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetShowNotificationsAsync(bool on, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetDebugLoggingAsync(bool on, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetCrashReportingAsync(bool on, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetTelemetryAsync(bool on, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetShowHintsAsync(bool on, CancellationToken ct = default) => Task.FromResult(config);
        public Task<VpnConfig> SetBoundIfOverrideAsync(string iface, CancellationToken ct = default) => Task.FromResult(config);
        public Task<string> CreateRouteScriptAsync(CancellationToken ct = default) => Task.FromResult("");
    }

    private static ConfigViewModel MakeVm(VpnConfig config)
    {
        var root = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        // Свои временные пути для автоподключения/автозапуска, как в ConfigViewStartupTests:
        // прогон тестов не должен трогать настоящие ~/.config/vantah/autoconnect и autostart.
        var autoConnect = new AutoConnectStore(Path.Combine(root, "autoconnect"));
        var autostart = new AutostartService(Path.Combine(root, "autostart"), "vantah", "vantah");
        return new ConfigViewModel(
            new ScriptedConfigService(config),
            new AppStateStore(),
            new Vantah.Core.Localization.LanguageStore(Path.Combine(root, "language")),
            new FakeUpdateChecker(),
            new FakeLogExporter(),
            () => Task.FromResult<string?>(null),
            autoConnect,
            autostart);
    }

    [AvaloniaFact]
    public async Task Successful_load_populates_the_form()
    {
        var config = new VpnConfig
        {
            Mode = VpnMode.Socks,
            SocksPort = 8899,
            SocksHost = "127.0.0.1",
            Protocol = VpnProtocol.Quic,
            UpdateChannel = UpdateChannel.Beta,
        };
        var vm = MakeVm(config);

        await vm.LoadTask;

        Assert.Equal("8899", vm.SocksPort);
        Assert.Equal("127.0.0.1", vm.SocksHost);
        Assert.Equal("quic", vm.SelectedProtocol);
        Assert.Equal("beta", vm.SelectedChannel);
        Assert.True(vm.IsSocksMode);
        Assert.Null(vm.Error);
    }

    // Заполнение формы, привязанной к отрисованным полям, при асинхронном ответе CLI
    // не должно падать и обязано наполнить форму — это и есть регресс cross-thread правки.
    [AvaloniaFact]
    public async Task Rendered_form_populates_on_async_load()
    {
        var config = new VpnConfig { SocksPort = 4242 };
        var vm = MakeVm(config);
        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 600, Height = 900 };
        window.Show();

        await vm.LoadTask;

        Assert.Equal("4242", vm.SocksPort);
        Assert.Null(vm.Error);
    }
}
