using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.App.ViewModels;
using Vantah.Core.Favorites;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.Tray;

public sealed class TrayIconController
{
    private const int DomainsTabIndex = 2;
    private readonly TrayIcon _icon;

    // Пункты со статичными подписями — их обновляет ApplyLabels() при смене языка.
    private readonly NativeMenuItem _fastest = new();
    private readonly NativeMenuItem _locations = new();
    private readonly NativeMenuItem _show = new();
    private readonly NativeMenuItem _exit = new();
    private readonly NativeMenuItem? _noFavorites;

    // Пункты, чья подпись зависит от состояния, — их ставит Apply(snapshot).
    private readonly NativeMenuItem _statusItem = new() { IsEnabled = false };
    private readonly NativeMenuItem _toggle = new();
    private readonly NativeMenuItem _domainsItem = new();

    private readonly VpnCoordinator _coordinator;
    private readonly TrayIconSet _icons;

    public TrayIconController(
        VpnCoordinator coordinator,
        AppStateStore store,
        FavoritesStore favorites,
        Window window,
        TrayIconSet icons)
    {
        _coordinator = coordinator;
        _icons = icons;
        _icon = new TrayIcon
        {
            ToolTipText = "Vantah",
            Icon = icons.For(store.Current.Connection),
        };

        _fastest.Click += async (_, _) => await _coordinator.ConnectAsync(null, true);
        _toggle.Click += async (_, _) => await OnToggle(store);

        _locations.Menu = BuildFavoritesMenu(favorites, out _noFavorites);

        _domainsItem.Click += (_, _) => ShowDomains(window);

        _show.Click += (_, _) => { window.Show(); window.Activate(); };

        _exit.Click += (_, _) => (Application.Current!.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.Shutdown();

        var menu = new NativeMenu();
        menu.Add(_statusItem);          // статус вверху
        menu.Add(_fastest);
        menu.Add(_locations);
        menu.Add(_domainsItem);         // кликабельно → окно на вкладке «Домены»
        menu.Add(_show);
        menu.Add(_toggle);              // Отключить/Подключить — внизу, перед выходом
        menu.Add(new NativeMenuItemSeparator());  // единственная отбивка — у выхода
        menu.Add(_exit);
        _icon.Menu = menu;

        _icon.Clicked += (_, _) => { window.Show(); window.Activate(); };

        // Регистрируем иконку в приложении, чтобы она отобразилась в трее.
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _icon });

        ApplyLabels();
        store.Changed += (_, s) => Dispatcher.UIThread.Post(() => Apply(s));
        Apply(store.Current);

        // Меню трея живёт всё время работы приложения и само не перечитывается —
        // после смены языка переставляем и статичные подписи, и зависящие от состояния.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            ApplyLabels();
            Apply(store.Current);
        });
    }

    // Подменю строится один раз при старте из сохранённого избранного.
    // Новые отмеченные звёздочкой локации появляются после перезапуска — приемлемо для MVP.
    private NativeMenu BuildFavoritesMenu(FavoritesStore favorites, out NativeMenuItem? placeholder)
    {
        var submenu = new NativeMenu();
        var keys = favorites.Load();
        if (keys.Count == 0)
        {
            // Подпись-заглушка локализуется вместе с остальными статичными пунктами.
            placeholder = new NativeMenuItem { IsEnabled = false };
            submenu.Add(placeholder);
            return submenu;
        }

        placeholder = null;
        foreach (var key in keys)
        {
            // Key = "ISO|City" — берём город после разделителя.
            var sep = key.IndexOf('|');
            var city = sep >= 0 ? key[(sep + 1)..] : key;
            var item = new NativeMenuItem(city);
            item.Click += async (_, _) => await _coordinator.ConnectAsync(city, fastest: false);
            submenu.Add(item);
        }
        return submenu;
    }

    private async Task OnToggle(AppStateStore store)
    {
        if (store.Current.Connection == ConnectionState.Connected)
            await _coordinator.DisconnectAsync();
        else
            await _coordinator.ConnectAsync(null, false);
    }

    // Клик по «Домены (N)» — показать окно и сразу открыть вкладку «Домены».
    private static void ShowDomains(Window window)
    {
        if (window.DataContext is MainWindowViewModel vm) vm.SelectedTab = DomainsTabIndex;
        window.Show();
        window.Activate();
    }

    /// <summary>Подписи пунктов, не зависящих от состояния VPN.</summary>
    private void ApplyLabels()
    {
        var loc = Localizer.Instance;
        _fastest.Header = loc[LocKeys.Tray_Fastest];
        _locations.Header = loc[LocKeys.Tray_Location];
        _show.Header = loc[LocKeys.Tray_ShowWindow];
        _exit.Header = loc[LocKeys.Tray_Exit];
        if (_noFavorites is { } item) item.Header = loc[LocKeys.Tray_NoFavorites];
    }

    private void Apply(AppSnapshot s)
    {
        var loc = Localizer.Instance;
        _icon.Icon = _icons.For(s.Connection);

        var connected = s.Connection == ConnectionState.Connected;
        _toggle.Header = loc[connected ? LocKeys.Common_Disconnect : LocKeys.Common_Connect];

        var glyph = connected ? "🟢" : "⚪";
        if (connected)
        {
            // Статус вверху меню: к чему и в каком режиме подключено.
            var mode = s.Mode is { } m ? $" · {m}" : "";
            _statusItem.Header = loc.Format(LocKeys.Tray_StatusConnectedFormat, glyph, s.Location ?? "", mode).Trim();

            var city = s.Location is { } l ? $": {l}" : "";
            var tip = loc.Format(LocKeys.Tray_Tooltip_ConnectedFormat, glyph, city);
            if (s.Traffic is { } t)
                tip += "\n" + loc.Format(
                    LocKeys.Tray_Tooltip_TrafficFormat, Format(t.RxBytesPerSec), Format(t.TxBytesPerSec));
            _icon.ToolTipText = tip;
        }
        else
        {
            _statusItem.Header = $"{glyph} {loc[LocKeys.Status_Disconnected]}";
            _icon.ToolTipText = loc.Format(LocKeys.Tray_Tooltip_DisconnectedFormat, glyph);

            // Ошибку не показываем глифом (на 16px не читается) — значит, она обязана быть здесь.
            if (s.Connection == ConnectionState.Error && !string.IsNullOrWhiteSpace(s.Error))
                _icon.ToolTipText += "\n" + loc.Format(LocKeys.Tray_Tooltip_ErrorFormat, s.Error);
        }

        // Счётчик доменов-исключений: пункт меню + строка в подсказке.
        // Дописывается в самом конце, чтобы не накапливаться поверх базового текста.
        _domainsItem.Header = loc.Format(LocKeys.Tray_DomainsFormat, s.ExclusionsCount);
        _icon.ToolTipText += "\n" + loc.Format(LocKeys.Tray_Tooltip_DomainsFormat, s.ExclusionsCount);
    }

    private static string Format(double bytes)
    {
        string[] u = { "B", "KB", "MB", "GB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.0} {u[i]}";
    }
}
