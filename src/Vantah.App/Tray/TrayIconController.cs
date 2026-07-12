using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Vantah.App.Services;
using Vantah.Core.Favorites;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.Tray;

public sealed class TrayIconController
{
    private readonly TrayIcon _icon;
    private readonly NativeMenuItem _toggle = new("Подключить");
    private readonly NativeMenuItem _domainsItem = new("Домены (0)") { IsEnabled = false };
    private readonly VpnCoordinator _coordinator;

    public TrayIconController(VpnCoordinator coordinator, AppStateStore store, FavoritesStore favorites, Window window)
    {
        _coordinator = coordinator;
        _icon = new TrayIcon
        {
            ToolTipText = "Vantah",
            Icon = LoadIcon(),
        };

        var fastest = new NativeMenuItem("⚡ Самая быстрая");
        fastest.Click += async (_, _) => await _coordinator.ConnectAsync(null, true);
        _toggle.Click += async (_, _) => await OnToggle(store);

        var locations = new NativeMenuItem("Локация") { Menu = BuildFavoritesMenu(favorites) };

        var show = new NativeMenuItem("Показать окно");
        show.Click += (_, _) => { window.Show(); window.Activate(); };

        var exit = new NativeMenuItem("Выход");
        exit.Click += (_, _) => (Application.Current!.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.Shutdown();

        var menu = new NativeMenu();
        menu.Add(_toggle);
        menu.Add(fastest);
        menu.Add(locations);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(_domainsItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(show);
        menu.Add(exit);
        _icon.Menu = menu;

        _icon.Clicked += (_, _) => { window.Show(); window.Activate(); };

        // Регистрируем иконку в приложении, чтобы она отобразилась в трее.
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _icon });

        store.Changed += (_, s) => Dispatcher.UIThread.Post(() => Apply(s));
        Apply(store.Current);
    }

    // Подменю строится один раз при старте из сохранённого избранного.
    // Новые отмеченные звёздочкой локации появляются после перезапуска — приемлемо для MVP.
    private NativeMenu BuildFavoritesMenu(FavoritesStore favorites)
    {
        var submenu = new NativeMenu();
        var keys = favorites.Load();
        if (keys.Count == 0)
        {
            submenu.Add(new NativeMenuItem("Нет избранного") { IsEnabled = false });
            return submenu;
        }

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

    private static WindowIcon? LoadIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Vantah.App/Assets/tray.ico"));
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }

    private async Task OnToggle(AppStateStore store)
    {
        if (store.Current.Connection == ConnectionState.Connected)
            await _coordinator.DisconnectAsync();
        else
            await _coordinator.ConnectAsync(null, false);
    }

    private void Apply(AppSnapshot s)
    {
        var connected = s.Connection == ConnectionState.Connected;
        _toggle.Header = connected ? "Отключить" : "Подключить";

        var glyph = connected ? "🟢" : "⚪";
        if (connected)
        {
            var loc = s.Location is { } l ? $": {l}" : "";
            var tip = $"{glyph} Vantah — Подключено{loc}";
            if (s.Traffic is { } t)
                tip += $"\n↓ {Format(t.RxBytesPerSec)}/s ↑ {Format(t.TxBytesPerSec)}/s";
            _icon.ToolTipText = tip;
        }
        else
        {
            _icon.ToolTipText = $"{glyph} Vantah — Отключено";
        }

        // Счётчик доменов-исключений: неактивный пункт меню + строка в подсказке.
        // Дописывается в самом конце, чтобы не накапливаться поверх базового текста.
        _domainsItem.Header = $"Домены ({s.ExclusionsCount})";
        _icon.ToolTipText += $"\nДомены: {s.ExclusionsCount}";
    }

    private static string Format(double bytes)
    {
        string[] u = { "B", "KB", "MB", "GB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.0} {u[i]}";
    }
}
