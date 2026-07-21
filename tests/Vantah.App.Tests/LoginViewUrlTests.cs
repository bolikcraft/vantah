using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Auth;
using Vantah.Core.History;
using Vantah.Core.Models;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;
using Xunit;

/// <summary>
/// Запасной путь входа: если браузер не открылся (нет xdg-open, отказ по схеме), ссылку должно
/// быть видно на экране и можно скопировать. Зелёная сборка этого не докажет (память проекта:
/// кнопки-пустышки компилировались), поэтому поднимаем настоящий LoginView headless.
/// </summary>
public class LoginViewUrlTests
{
    private const string Url = "https://host.test/device_code?user_code=AAAA-BBBB";

    private static (LoginViewModel Vm, List<string> Copied) NewVm(FakeAuthService auth)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vantah-tests", System.Guid.NewGuid().ToString("N"));
        var store = new AppStateStore();
        var coordinator = new VpnCoordinator(
            new FakeVpnService(), new TrafficMonitor(new FakeTrafficReader()), store,
            new ConnectionHistoryTracker(
                new ConnectionHistoryStore(Path.Combine(temp, "history")),
                new ActiveSessionStore(Path.Combine(temp, "connection-active"))),
            new IpVersionStore(Path.Combine(temp, "ip-version")), auth);

        var copied = new List<string>();
        var vm = new LoginViewModel(auth, coordinator)
        {
            BrowserOpener = _ => Task.CompletedTask,
            ClipboardWriter = t => { copied.Add(t); return Task.CompletedTask; },
        };
        return (vm, copied);
    }

    [AvaloniaFact]
    public async Task Copy_command_puts_the_url_into_the_clipboard()
    {
        var auth = new FakeAuthService
        {
            State = LoginState.LoggedIn,
            Prompt = new DeviceCodePrompt(Url, "AAAA-BBBB", 600),
        };
        var (vm, copied) = NewVm(auth);

        await vm.StartCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        await vm.CopyUrlCommand.ExecuteAsync(null);

        Assert.Equal(new[] { Url }, copied);
    }

    [AvaloniaFact]
    public async Task Copy_command_does_nothing_without_a_url()
    {
        var (vm, copied) = NewVm(new FakeAuthService { State = LoginState.LoggedOut });

        await vm.CopyUrlCommand.ExecuteAsync(null);

        Assert.Empty(copied);
    }

    [AvaloniaFact]
    public async Task View_shows_the_link_and_a_copy_button_once_the_url_is_known()
    {
        var auth = new FakeAuthService
        {
            State = LoginState.LoggedOut,          // остаёмся на экране ожидания
            Prompt = new DeviceCodePrompt(Url, "AAAA-BBBB", 600),
        };
        var (vm, copied) = NewVm(auth);
        var window = new Window { Content = new LoginView { DataContext = vm }, Width = 500, Height = 500 };
        window.Show();
        window.UpdateLayout();

        var block = UrlBlock(window);
        Assert.False(block.IsVisible);             // ссылки ещё нет — прятать

        await vm.StartCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        vm.IsAwaitingAuth = true;                  // фейк доходит до конца входа мгновенно
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.True(block.IsVisible);
        var text = block.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        Assert.Equal(Url, text.Text);

        // Кнопка не должна быть пустышкой: жмём её команду и смотрим на буфер.
        var button = block.GetVisualDescendants().OfType<Button>()
            .Single(b => (b.Content as string) == Localizer.Instance[LocKeys.Login_CopyUrl]);
        button.Command!.Execute(button.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new[] { Url }, copied);
    }

    private static StackPanel UrlBlock(Window w) =>
        w.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "LoginUrlBlock");
}
