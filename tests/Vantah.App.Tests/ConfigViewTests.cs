using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Vantah.App.Localization;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Localization;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.Settings;
using Vantah.Core.State;
using Vantah.Core.Update;
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

    // LanguageStore пишет в ~/.config/vantah/language — путь уводим в temp,
    // иначе прогон тестов перезаписал бы настоящий выбор языка пользователя.
    private static ConfigViewModel Vm(
        FakeConfigService svc,
        AppStateStore? store = null,
        IUpdateChecker? updates = null,
        ILogExporter? logs = null,
        Func<Task<string?>>? folderPicker = null) =>
        new(svc,
            store ?? new AppStateStore(),
            new LanguageStore(Path.Combine(
                Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "language")),
            updates ?? new FakeUpdateChecker(),
            logs ?? new FakeLogExporter(),
            folderPicker ?? (() => Task.FromResult<string?>(null)));

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
        Assert.Equal(10, boxes.Length);  // +3 за карточку «Запуск»: автоподключение, автозапуск, проверка обновлений
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

    [AvaloniaFact]
    public void Route_script_button_visible_only_in_script_routing()
    {
        var svc = new FakeConfigService(new VpnConfig { TunnelRoutingMode = TunnelRoutingMode.Auto });
        var vm = Vm(svc);
        Show(vm);

        Assert.False(vm.IsRouteScriptAvailable);

        vm.SelectedRouting = "script";

        Assert.True(vm.IsRouteScriptAvailable);
    }

    [AvaloniaFact]
    public async Task Creating_route_script_calls_the_service()
    {
        var svc = new FakeConfigService(new VpnConfig { TunnelRoutingMode = TunnelRoutingMode.Script });
        var vm = Vm(svc);
        Show(vm);

        await vm.CreateRouteScriptCommand.ExecuteAsync(null);

        Assert.Contains("create-route-script", svc.Calls);
        Assert.NotNull(vm.StatusText);
    }

    // Собственные кнопки формы: порт, хост, save-auth, clear-auth, DNS, интерфейс,
    // route-скрипт, обновить, выгрузить логи = 9.
    [AvaloniaFact]
    public void All_own_buttons_still_wired()
    {
        var svc = new FakeConfigService(new VpnConfig { TunnelRoutingMode = TunnelRoutingMode.Script });
        var window = Show(Vm(svc));

        var buttons = OwnButtons(window);

        Assert.Equal(9, buttons.Length);
        Assert.All(buttons, b => Assert.NotNull(b.Command));
    }

    /// <summary>
    /// Жмём именно КНОПКУ, а не команду вьюмодели: проверяем проводку разметки.
    /// По имени, а не по подписи: у DNS и интерфейса общая подпись «Применить».
    /// </summary>
    private static async Task ClickAsync(Window window, string name)
    {
        var button = OwnButtons(window).Single(b => b.Name == name && b.IsEffectivelyVisible);

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
        await ClickAsync(window, "ApplyDnsButton");

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
        await ClickAsync(window, "ApplyDnsButton");

        Assert.Contains("set-dns:94.140.14.14", svc.Calls);
    }

    /// <summary>
    /// Язык интерфейса — поле формы настроек, а не элемент полосы вкладок. Выбор в комбобоксе
    /// обязан переключать язык на лету: проверяем через сам ComboBox, а не через вьюмодель,
    /// иначе отвязанный SelectedItem остался бы незамеченным.
    /// </summary>
    [AvaloniaFact]
    public void Choosing_a_language_switches_the_ui_language()
    {
        var vm = Vm(new FakeConfigService());
        var window = Show(vm);

        var combo = window.GetVisualDescendants().OfType<ComboBox>()
            .Single(c => c.ItemsSource is IEnumerable<LanguageOption>);

        try
        {
            combo.SelectedItem = vm.Languages.Single(l => l.Code == "en");

            Assert.Equal("en", Localizer.Instance.Language);
            Assert.Equal("en", vm.SelectedLanguage.Code);
        }
        finally
        {
            // Localizer.Instance — синглтон на весь тест-хост: не вернём «ru» — протечёт в другие тесты.
            Localizer.Instance.SetLanguage("ru");
        }
    }

    /// <summary>
    /// Выбор языка обязан пережить перезапуск: комбобокс не только переключает язык на лету,
    /// но и пишет код в LanguageStore, откуда его читает следующий старт приложения.
    /// </summary>
    [AvaloniaFact]
    public void Choosing_a_language_saves_it_for_the_next_launch()
    {
        // Свой путь, а не ~/.config/vantah/language: прогон тестов не должен трогать настоящий выбор.
        var path = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "language");
        var vm = new ConfigViewModel(
            new FakeConfigService(), new AppStateStore(), new LanguageStore(path),
            new FakeUpdateChecker(), new FakeLogExporter(), () => Task.FromResult<string?>(null));
        var window = Show(vm);

        var combo = window.GetVisualDescendants().OfType<ComboBox>()
            .Single(c => c.ItemsSource is IEnumerable<LanguageOption>);

        try
        {
            combo.SelectedItem = vm.Languages.Single(l => l.Code == "en");

            Assert.True(File.Exists(path));
            Assert.Equal("en", File.ReadAllText(path).Trim());
            Assert.Equal("en", new LanguageStore(path).Load());
        }
        finally
        {
            Localizer.Instance.SetLanguage("ru");
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void Toggling_crash_reporting_applies_it()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        Show(vm);

        vm.CrashReporting = true;

        Assert.Contains("set-crash-reporting:True", svc.Calls);
    }

    [AvaloniaFact]
    public void Toggling_telemetry_applies_it()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        Show(vm);

        vm.Telemetry = false;   // стартовое значение может быть false → no-op
        vm.Telemetry = true;

        Assert.Contains("set-telemetry:True", svc.Calls);
    }

    [AvaloniaFact]
    public async Task Applying_interface_override_sends_trimmed_value()
    {
        var svc = new FakeConfigService();
        var vm = Vm(svc);
        Show(vm);

        vm.InterfaceOverride = "  eth0  ";
        await vm.ApplyInterfaceOverrideCommand.ExecuteAsync(null);

        Assert.Contains("set-bound-if-override:eth0", svc.Calls);
    }

    [AvaloniaFact]
    public void Warning_banner_appears_only_while_connected()
    {
        var store = new AppStateStore();
        var vm = Vm(new FakeConfigService(), store);
        var window = Show(vm);

        var banner = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Child is TextBlock t &&
                        t.Text == Localizer.Instance[LocKeys.Settings_ConnectedWarning]);
        Assert.False(banner.IsEffectivelyVisible);

        vm.IsConnectedWarningVisible = true;

        Assert.True(banner.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Update_banner_shown_when_a_newer_version_exists()
    {
        var vm = Vm(new FakeConfigService(),
                    updates: new FakeUpdateChecker(new UpdateStatus(false, "1.5.2")));
        Show(vm);

        await vm.UpdateCheckTask;

        Assert.True(vm.IsUpdateAvailable);
        Assert.Contains("1.5.2", vm.UpdateMessage);
    }

    [AvaloniaFact]
    public async Task No_update_banner_when_latest()
    {
        var vm = Vm(new FakeConfigService(), updates: new FakeUpdateChecker(new UpdateStatus(true, null)));
        Show(vm);

        await vm.UpdateCheckTask;

        Assert.False(vm.IsUpdateAvailable);
    }

    [AvaloniaFact]
    public async Task Exporting_logs_uses_the_picked_folder()
    {
        var logs = new FakeLogExporter();
        var vm = Vm(new FakeConfigService(), logs: logs,
                    folderPicker: () => Task.FromResult<string?>("/tmp/vantah-logs"));
        Show(vm);

        await vm.ExportLogsCommand.ExecuteAsync(null);

        Assert.Contains("/tmp/vantah-logs", logs.Exported);
        Assert.NotNull(vm.StatusText);
    }

    [AvaloniaFact]
    public async Task Cancelled_folder_pick_exports_nothing()
    {
        var logs = new FakeLogExporter();
        var vm = Vm(new FakeConfigService(), logs: logs,
                    folderPicker: () => Task.FromResult<string?>(null));
        Show(vm);

        await vm.ExportLogsCommand.ExecuteAsync(null);

        Assert.Empty(logs.Exported);
    }
}
