using Vantah.Core.Errors;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Favorites;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;
using Location = Vantah.Core.Models.Location;

/// <summary>
/// Загрузка списка локаций: наполнение, состояние «Загрузка…», ошибка + «Обновить».
/// Баг из практики — список молча пустой, потому что заполнение шло не на UI-потоке;
/// здесь проверяем наблюдаемый контракт загрузки (значения не зависят от потока продолжения).
/// </summary>
public class LocationsViewTests
{
    // list-locations, отвечающий АСИНХРОННО (через Task.Yield) — в отличие от синхронного
    // FakeVpnService. Каждый вызов берёт следующий сценарий (последний повторяется).
    private sealed class ScriptedVpn(params Func<IReadOnlyList<Location>>[] steps) : IVpnService
    {
        private int _i;
        public int Calls { get; private set; }

        public async Task<IReadOnlyList<Location>> GetLocationsAsync(int count = 300, CancellationToken ct = default)
        {
            Calls++;
            await Task.Yield();
            return steps[Math.Min(_i++, steps.Length - 1)]();
        }

        public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<VpnStatus> ConnectAsync(string? location, bool fastest,
            IpVersionPreference ipVersion = IpVersionPreference.Auto,
            bool killSwitch = false, CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<VpnStatus> DisconnectAsync(CancellationToken ct = default) =>
            Task.FromResult(VpnStatus.Disconnected);
        public Task<License> GetLicenseAsync(CancellationToken ct = default) =>
            Task.FromResult(new License("", "", 0, null));
        public Task<string?> GetCliVersionAsync(CancellationToken ct = default) =>
            Task.FromResult<string?>("test");
    }

    private static Location Loc(string iso, string country, string city, int ping) =>
        new(iso, country, city, ping);

    private static LocationsViewModel MakeVm(IVpnService vpn, AppStateStore? store = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        store ??= new AppStateStore();
        var traffic = new TrafficMonitor(new FakeTrafficReader());
        var history = new ConnectionHistoryTracker(
            new ConnectionHistoryStore(Path.Combine(dir, "connections-history")),
            new ActiveSessionStore(Path.Combine(dir, "connection-active")));
        var ipStore = new IpVersionStore(Path.Combine(dir, "ip-version"));
        var coord = new VpnCoordinator(vpn, traffic, store, history, ipStore, new FakeAuthService());
        var favorites = new FavoritesStore(Path.Combine(dir, "favorites"));
        return new LocationsViewModel(vpn, coord, favorites, store);
    }

    [AvaloniaFact]
    public async Task Successful_load_populates_items_and_ends_loading()
    {
        var vm = MakeVm(new ScriptedVpn(() => new[]
        {
            Loc("NL", "Netherlands", "Amsterdam", 10),
            Loc("DE", "Germany", "Berlin", 20),
        }));

        await vm.LoadTask;

        Assert.Equal(2, vm.Items.Count);
        Assert.False(vm.IsLoading);
        Assert.Null(vm.LoadError);
    }

    [AvaloniaFact]
    public async Task Failed_load_surfaces_error_and_ends_loading()
    {
        var vm = MakeVm(new ScriptedVpn(() => throw new VpnCommandException(AppError.Cli("не выполнен вход"))));

        await vm.LoadTask;

        Assert.Empty(vm.Items);
        Assert.False(vm.IsLoading);
        Assert.Equal("не выполнен вход", vm.LoadError);
    }

    // Пока статус подключения не определён (Unknown — до первого ответа CLI), кнопки
    // «Подключить/Отключить» заблокированы: список не должен предлагать подключиться, когда
    // туннель, возможно, уже поднят. Как только статус известен — действия доступны.
    [AvaloniaFact]
    public async Task Row_actions_stay_disabled_until_status_is_known()
    {
        var store = new AppStateStore();
        var vm = MakeVm(new ScriptedVpn(() => new[] { Loc("NL", "Netherlands", "Amsterdam", 10) }), store);

        await vm.LoadTask;
        Assert.False(vm.IsStatusKnown);   // статус ещё Unknown — кнопки строк заблокированы

        store.Set(s => s with { Connection = ConnectionState.Disconnected });
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.IsStatusKnown);    // статус определился — кнопки строк доступны
    }

    // Заполнение списка, привязанного к отрисованному ListBox, при асинхронном ответе CLI
    // не должно падать и обязано наполнить список — это и есть регресс cross-thread правки.
    [AvaloniaFact]
    public async Task Rendered_list_populates_on_async_load()
    {
        var vm = MakeVm(new ScriptedVpn(() => new[] { Loc("NL", "Netherlands", "Amsterdam", 10) }));
        var window = new Window { Content = new LocationsView { DataContext = vm }, Width = 800, Height = 600 };
        window.Show();

        await vm.LoadTask;

        Assert.Null(vm.LoadError);
        Assert.Single(vm.Items);
    }

    [AvaloniaFact]
    public async Task Reload_recovers_after_a_failure()
    {
        var vpn = new ScriptedVpn(
            () => throw new VpnCommandException(AppError.Cli("сеть недоступна")),
            () => new[] { Loc("DE", "Germany", "Berlin", 20) });
        var vm = MakeVm(vpn);
        await vm.LoadTask;
        Assert.Equal("сеть недоступна", vm.LoadError);

        await vm.ReloadCommand.ExecuteAsync(null);

        Assert.Null(vm.LoadError);
        Assert.Single(vm.Items);
        Assert.Equal(2, vpn.Calls);
    }
}
