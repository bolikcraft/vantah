using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.History;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

/// <summary>
/// С kill switch CLI зовётся с <c>--boot</c> и ретраит подключение бесконечно, пока нет сети,
/// поэтому <see cref="ConnectionState.Connecting"/> может держаться сколько угодно. Раньше
/// кнопка на «Статусе» в этом состоянии была заблокирована (IsBusy), и прервать подключение
/// из окна было нельзя. Теперь она активна и работает как «Прервать подключение».
/// </summary>
public class StatusViewModelCancelConnectTests
{
    /// <summary>Считает вызовы disconnect: общий FakeVpnService их не записывает.</summary>
    private sealed class RecordingVpnService : IVpnService
    {
        public int Connects { get; private set; }
        public int Disconnects { get; private set; }

        public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
            IpVersionPreference ipVersion = IpVersionPreference.Auto,
            bool killSwitch = false, CancellationToken ct = default)
        {
            Connects++;
            return Task.FromResult(VpnStatus.Disconnected);
        }

        public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default)
        {
            Disconnects++;
            return Task.FromResult(VpnStatus.Disconnected);
        }

        public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<IReadOnlyList<Vantah.Core.Models.Location>> GetLocationsAsync(
            int count = 300, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Vantah.Core.Models.Location>>([]);
        public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
            Task.FromResult(new License("", "", 0, null));
        public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>("test");
    }

    [AvaloniaFact]
    public void Connecting_leaves_the_button_enabled_and_offers_to_stop()
    {
        var (vm, store, _) = MakeStatusVm();

        store.Set(s => s with { Connection = ConnectionState.Connecting });
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsBusy);
        Assert.Equal(Localizer.Instance[LocKeys.Common_StopConnecting], vm.ToggleText);
    }

    [AvaloniaFact]
    public async Task Pressing_the_button_while_connecting_disconnects()
    {
        var (vm, store, vpn) = MakeStatusVm();

        store.Set(s => s with { Connection = ConnectionState.Connecting });
        Dispatcher.UIThread.RunJobs();
        await vm.ToggleCommand.ExecuteAsync(null);

        Assert.Equal(1, vpn.Disconnects);
        Assert.Equal(0, vpn.Connects);
    }

    [AvaloniaFact]
    public async Task Connected_still_offers_to_disconnect()
    {
        var (vm, store, vpn) = MakeStatusVm();

        store.Set(s => s with { Connection = ConnectionState.Connected });
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsBusy);
        Assert.Equal(Localizer.Instance[LocKeys.Common_Disconnect], vm.ToggleText);

        await vm.ToggleCommand.ExecuteAsync(null);

        Assert.Equal(1, vpn.Disconnects);
        Assert.Equal(0, vpn.Connects);
    }

    [AvaloniaFact]
    public async Task Disconnected_still_offers_to_connect()
    {
        var (vm, store, vpn) = MakeStatusVm();

        store.Set(s => s with { Connection = ConnectionState.Disconnected });
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsBusy);
        Assert.Equal(Localizer.Instance[LocKeys.Common_Connect], vm.ToggleText);

        await vm.ToggleCommand.ExecuteAsync(null);

        Assert.Equal(1, vpn.Connects);
        Assert.Equal(0, vpn.Disconnects);
    }

    [AvaloniaTheory]
    [InlineData(ConnectionState.Disconnecting)]
    [InlineData(ConnectionState.Unknown)]
    public void Other_transient_states_still_block_the_button(ConnectionState state)
    {
        var (vm, store, _) = MakeStatusVm();

        store.Set(s => s with { Connection = state });
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsBusy);
    }

    /// <summary>Без сети `disconnect` отвечает до 10 c, и всё это время кнопка неактивна.
    /// Подпись «Подключить» на ней читалась как зависшее окно, поэтому подписываем процессом.</summary>
    [AvaloniaFact]
    public void Disconnecting_labels_the_blocked_button_with_the_process()
    {
        var (vm, store, _) = MakeStatusVm();

        store.Set(s => s with { Connection = ConnectionState.Disconnecting });
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsBusy);
        Assert.Equal(Localizer.Instance[LocKeys.Status_Disconnecting], vm.ToggleText);
        Assert.NotEqual(Localizer.Instance[LocKeys.Common_Connect], vm.ToggleText);
    }

    /// <summary>
    /// Зелёная сборка Avalonia не доказывает, что кнопка на экране разблокировалась: биндинги
    /// IsEnabled/Content рвутся молча. Гоняем настоящий StatusView в headless-окне.
    /// </summary>
    [AvaloniaFact]
    public void The_view_enables_the_button_while_connecting_and_blocks_it_while_disconnecting()
    {
        var (vm, store, _) = MakeStatusVm();
        var window = new Window { Content = new StatusView { DataContext = vm }, Width = 600, Height = 900 };
        window.Show();

        store.Set(s => s with { Connection = ConnectionState.Connecting });
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var button = ToggleButton(window);
        Assert.True(button.IsEnabled);
        Assert.Equal(Localizer.Instance[LocKeys.Common_StopConnecting], button.Content);

        store.Set(s => s with { Connection = ConnectionState.Disconnecting });
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.False(button.IsEnabled);
    }

    /// <summary>Подпись собирается в коде, поэтому после смены языка её надо пересобрать.</summary>
    [AvaloniaFact]
    public void The_stop_label_follows_a_language_switch()
    {
        var (vm, store, _) = MakeStatusVm();
        var window = new Window { Content = new StatusView { DataContext = vm }, Width = 600, Height = 900 };
        window.Show();

        try
        {
            store.Set(s => s with { Connection = ConnectionState.Connecting });
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Stop connecting", ToggleButton(window).Content);

            Localizer.Instance.SetLanguage("ru");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Прервать подключение", ToggleButton(window).Content);
        }
        finally
        {
            // Localizer.Instance — синглтон на весь тест-хост: не вернём язык по умолчанию —
            // протечёт в соседей.
            Localizer.Instance.SetLanguage(CultureSelector.Default);
        }
    }

    /// <summary>Кнопка Toggle из блока статуса (в скелетоне есть невидимый образец-двойник).</summary>
    private static Button ToggleButton(Window w) =>
        w.GetVisualDescendants().OfType<Button>().Single(b => b.Command is not null);

    private static (StatusViewModel Vm, AppStateStore Store, RecordingVpnService Vpn) MakeStatusVm()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var vpn = new RecordingVpnService();
        var coord = new VpnCoordinator(vpn, traffic, store, history, ipStore, new FakeAuthService());
        var logReader = new VpnLogReader(Path.Combine(dir, "vpn.log"));
        var vm = new StatusViewModel(coord, store, logReader, new HistoryViewModel(coord, store), ipStore);
        return (vm, store, vpn);
    }
}
