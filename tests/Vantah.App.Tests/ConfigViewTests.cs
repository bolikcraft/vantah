using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Models;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Xunit;

/// <summary>
/// Вкладка «Настройки» рендерится headless. Зелёная сборка не доказывает, что форма живая:
/// в E4 так уже проехали кнопки-пустышки, у которых Command оказывался null.
/// </summary>
public class ConfigViewTests
{
    private static Window Show(ConfigViewModel vm)
    {
        var window = new Window { Content = new ConfigView { DataContext = vm }, Width = 600, Height = 900 };
        window.Show();
        return window;
    }

    private static ConfigViewModel Vm(FakeConfigService svc, AppStateStore? store = null) =>
        new(svc, store ?? new AppStateStore());

    /// <summary>Кнопки самой формы: ровно <see cref="Button"/>, без ToggleButton/RepeatButton из шаблонов.</summary>
    private static Button[] OwnButtons(Window window) =>
        window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.GetType() == typeof(Button))
            .ToArray();

    [AvaloniaFact]
    public void Form_shows_the_values_read_from_the_cli()
    {
        var svc = new FakeConfigService(new VpnConfig
        {
            Mode = VpnMode.Socks,
            SocksPort = 8899,
            SocksHost = "127.0.0.1",
            Protocol = VpnProtocol.Quic,
            UpdateChannel = UpdateChannel.Beta,
            TunnelRoutingMode = TunnelRoutingMode.Script,
            PostQuantum = true,
            DebugLogging = true,
        });
        var vm = Vm(svc);

        var window = Show(vm);

        var texts = window.GetVisualDescendants().OfType<TextBox>().Select(t => t.Text).ToArray();
        Assert.Contains("8899", texts);
        Assert.Equal("quic", vm.SelectedProtocol);
        Assert.Equal("beta", vm.SelectedChannel);
        Assert.Equal("script", vm.SelectedRouting);

        var boxes = window.GetVisualDescendants().OfType<CheckBox>().ToArray();
        Assert.Equal(4, boxes.Length);
        Assert.Contains(boxes, b => b.IsChecked == true);   // post-quantum и debug пришли включёнными
    }

    // Гарантия от петли «прочитали → тут же записали обратно»: загрузка формы не шлёт set-*.
    [AvaloniaFact]
    public void Loading_the_form_does_not_write_anything_back()
    {
        var svc = new FakeConfigService(new VpnConfig { Mode = VpnMode.Socks, PostQuantum = true });

        Show(Vm(svc));

        Assert.Equal(["get"], svc.Calls);
    }

    [AvaloniaFact]
    public void Socks_section_is_hidden_in_tun_mode_and_shown_in_socks_mode()
    {
        var svc = new FakeConfigService(new VpnConfig { Mode = VpnMode.Tun });
        var vm = Vm(svc);
        var window = Show(vm);

        // Секцию SOCKS опознаём по полю пароля — оно есть только там.
        var password = () => window.GetVisualDescendants().OfType<TextBox>().Single(t => t.PasswordChar == '•');
        Assert.False(password().IsEffectivelyVisible);

        vm.IsSocksMode = true;

        Assert.True(password().IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void Toggling_a_checkbox_applies_it_to_the_cli()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        Show(vm);

        vm.PostQuantum = true;

        Assert.Contains("set-post-quantum:True", svc.Calls);
    }

    [AvaloniaFact]
    public void Switching_the_mode_toggle_applies_it_to_the_cli()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        Show(vm);

        vm.IsSocksMode = true;

        Assert.Contains("set-mode:Socks", svc.Calls);
    }

    [AvaloniaFact]
    public async Task Invalid_port_shows_an_error_and_sends_nothing()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        var window = Show(vm);

        vm.SocksPort = "70000";
        await vm.ApplySocksPortCommand.ExecuteAsync(null);

        Assert.DoesNotContain(svc.Calls, c => c.StartsWith("set-socks-port"));
        Assert.NotNull(vm.Error);
        // Ошибка не только во вьюмодели, но и на экране.
        var shown = window.GetVisualDescendants().OfType<TextBlock>()
            .Any(t => t.Text == vm.Error && t.IsEffectivelyVisible);
        Assert.True(shown);
    }

    /// <summary>
    /// Каждая кнопка формы связана с командой. В E4 кнопка, отвязанная от вьюмодели, молча
    /// становилась пустышкой: сборка зелёная, тесты через vm.Command зелёные, клик — ничего.
    /// </summary>
    [AvaloniaFact]
    public void Every_button_is_wired_to_a_command()
    {
        var window = Show(Vm(new FakeConfigService { }));

        // Ровно Button, без наследников: шаблоны ComboBox и ScrollViewer полны служебных
        // ToggleButton/RepeatButton, у которых команды и не бывает.
        var buttons = OwnButtons(window);

        Assert.Equal(6, buttons.Length);   // порт, хост, сохранить auth, сбросить auth, DNS, обновить
        Assert.All(buttons, b => Assert.NotNull(b.Command));
    }

    /// <summary>Жмём именно КНОПКУ, а не команду вьюмодели: проверяем проводку разметки.</summary>
    private static async Task ClickAsync(Window window, string contentKey)
    {
        var label = Localizer.Instance[contentKey];
        var button = OwnButtons(window).Single(b => (b.Content as string) == label && b.IsEffectivelyVisible);

        Assert.NotNull(button.Command);
        button.Command!.Execute(button.CommandParameter);

        // Команда асинхронная: дать ей завершиться до проверок.
        if (button.Command is IAsyncRelayCommand async) await async.ExecutionTask!;
    }

    [AvaloniaFact]
    public async Task Empty_dns_field_resets_the_upstream_instead_of_failing()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        var window = Show(vm);

        vm.DnsUpstream = "";
        await ClickAsync(window, LocKeys.Common_Apply);   // единственная видимая «Применить» в режиме TUN — у DNS

        Assert.Contains("reset-dns", svc.Calls);
        Assert.Null(vm.Error);
    }

    [AvaloniaFact]
    public async Task Dns_upstream_is_sent_trimmed()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        var window = Show(vm);

        vm.DnsUpstream = "  94.140.14.14  ";
        await ClickAsync(window, LocKeys.Common_Apply);

        Assert.Contains("set-dns:94.140.14.14", svc.Calls);
    }

    [AvaloniaFact]
    public void Warning_banner_appears_only_while_connected()
    {
        var store = new AppStateStore();
        var vm = Vm(new FakeConfigService(), store);
        var window = Show(vm);

        var banner = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Child is TextBlock);
        Assert.False(banner.IsEffectivelyVisible);

        vm.IsConnectedWarningVisible = true;

        Assert.True(banner.IsEffectivelyVisible);
    }
}
