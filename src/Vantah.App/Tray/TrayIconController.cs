using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Vantah.App.Services;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.Tray;

public sealed class TrayIconController
{
    private readonly TrayIcon _icon;
    private readonly NativeMenuItem _toggle = new("Подключить");
    private readonly VpnCoordinator _coordinator;

    public TrayIconController(VpnCoordinator coordinator, AppStateStore store, Window window)
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

        var show = new NativeMenuItem("Показать окно");
        show.Click += (_, _) => { window.Show(); window.Activate(); };

        var exit = new NativeMenuItem("Выход");
        exit.Click += (_, _) => (Application.Current!.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.Shutdown();

        var menu = new NativeMenu();
        menu.Add(_toggle);
        menu.Add(fastest);
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
        _icon.ToolTipText = connected
            ? $"Vantah — Подключено{(s.Location is { } l ? $": {l}" : "")}"
            : "Vantah — Отключено";
    }
}
