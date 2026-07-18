using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.Core.Auth;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

/// <summary>
/// Device-code вход из UI: «Войти» получает ссылку (колбэк с фонового потока → Dispatcher) и
/// открывает браузер. Headless — потому что синхронный фейк скрыл бы thread-affinity (память проекта).
/// </summary>
public class LoginViewModelTests
{
    private static (LoginViewModel Vm, AppStateStore Store, List<string> Opened) NewVm(FakeAuthService auth)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vantah-tests", System.Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var coordinator = new VpnCoordinator(
            new FakeVpnService(), new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(temp, "history")),
                new ActiveSessionStore(Path.Combine(temp, "connection-active"))),
            new IpVersionStore(Path.Combine(temp, "ip-version")), auth);

        var opened = new List<string>();
        var vm = new LoginViewModel(auth, coordinator)
        {
            BrowserOpener = u => { opened.Add(u); return Task.CompletedTask; },
        };
        return (vm, store, opened);
    }

    [AvaloniaFact]
    public async Task Start_surfaces_device_code_and_opens_browser()
    {
        var auth = new FakeAuthService
        {
            State = LoginState.LoggedIn,   // после входа зонд увидит «залогинен»
            Prompt = new DeviceCodePrompt("https://host.test/device_code?user_code=AAAA-BBBB", "AAAA-BBBB", 600),
        };
        var (vm, store, opened) = NewVm(auth);

        await vm.StartCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("AAAA-BBBB", vm.UserCode);
        Assert.Equal("https://host.test/device_code?user_code=AAAA-BBBB", vm.Url);
        Assert.Contains("https://host.test/device_code?user_code=AAAA-BBBB", opened);   // авто-открытие
        Assert.Equal(LoginState.LoggedIn, store.Current.LoginState);                    // зонд обновил гейт
    }

    [AvaloniaFact]
    public async Task Failed_login_shows_error_and_does_not_stay_awaiting()
    {
        var auth = new FakeAuthService { State = LoginState.LoggedOut, NextResult = LoginResult.Fail("Вход отменён") };
        var (vm, _, _) = NewVm(auth);

        await vm.StartCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsAwaitingAuth);
        Assert.Equal("Вход отменён", vm.Error);
    }
}
