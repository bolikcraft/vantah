using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    private readonly VpnCoordinator _coordinator;
    private readonly AppStateStore _store;
    private readonly FavoritesStore _favorites;
    private readonly Window _window;
    private readonly TrayIconSet _icons;

    // Не readonly: пересоздание заменяет иконку целиком. Internal — за иконкой напрямую
    // смотрят тесты трея (InternalsVisibleTo), рефлексия для этого не нужна.
    internal TrayIcon _icon;

    // Пункты со статичными подписями — их обновляет ApplyLabels() при смене языка.
    // Без инициализаторов: единственный, кто создаёт пункты, — CreateIcon(), а конструктор
    // зовёт её первым делом. Что после неё поля не null, компилятору объясняет [MemberNotNull]
    // на самой CreateIcon — иначе здесь пришлось бы создавать девять пунктов, чтобы тут же их
    // выбросить.
    private NativeMenuItem _fastest;
    private NativeMenuItem _locations;
    private NativeMenuItem _show;
    private NativeMenuItem _exit;
    private NativeMenuItem? _noFavorites;

    // Пункты, чья подпись зависит от состояния, — их ставит Apply(snapshot).
    private NativeMenuItem _statusItem;
    private NativeMenuItem _toggle;
    private NativeMenuItem _domainsItem;

    public TrayIconController(
        VpnCoordinator coordinator,
        AppStateStore store,
        FavoritesStore favorites,
        Window window,
        TrayIconSet icons)
    {
        _coordinator = coordinator;
        _store = store;
        _favorites = favorites;
        _window = window;
        _icons = icons;

        _icon = CreateIcon(FavoriteCities());

        // Регистрируем иконку в приложении, чтобы она появилась в трее. Пересоздание делает то
        // же самое, но через TrayIconGuard.SwapAsync — там перед SetIcons надо снять старую.
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _icon });

        // Подписки ставятся ровно один раз, в конструкторе: они смотрят на текущие поля, а не
        // на конкретный TrayIcon, и потому переживают пересоздание без повторной регистрации.
        store.Changed += (_, s) => Dispatcher.UIThread.Post(() => Apply(s));

        // Пункты меню пересобираются вместе с иконкой, но сами по себе подписи не перечитывают —
        // после смены языка переставляем и статичные, и зависящие от состояния.
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            ApplyLabels();
            Apply(_store.Current);
        });
    }

    /// <summary>
    /// Пересоздаёт иконку трея вместе с её D-Bus-соединением — обход дублирования индикатора
    /// после блокировки экрана в GNOME (см. <see cref="TrayRestartPolicy"/>).
    /// </summary>
    public Task RebuildAsync() => UiThread.RunAsync(() => RebuildAsync(TrayDBusConnection.TryGet));

    // Собирает иконку и меню с нуля. Все пункты создаются заново: NativeMenuItem, однажды
    // добавленный в NativeMenu, второй раз в другое меню не добавляется (Avalonia бросает
    // «already has a parent»), поэтому переиспользовать старые нельзя.
    // В приложении иконку НЕ регистрирует: это делает вызывающий (конструктор — сразу,
    // пересоздание — из TrayIconGuard.SwapAsync, уже после снятия старой).
    // Готовую иконку и кладёт в _icon, и возвращает: возврат нужен конструктору, иначе
    // компилятор не видит присваивания поля (CS8618).
    // Список избранных городов — параметром: при пересоздании он читается с диска заранее,
    // до снятия старой иконки (см. RebuildAsync).
    [MemberNotNull(nameof(_fastest), nameof(_locations), nameof(_show), nameof(_exit),
                   nameof(_statusItem), nameof(_toggle), nameof(_domainsItem))]
    private TrayIcon CreateIcon(IReadOnlyList<string> favoriteCities)
    {
        var icon = new TrayIcon
        {
            ToolTipText = "Vantah",
            Icon = _icons.For(_store.Current.Connection),
        };

        _fastest = new NativeMenuItem();
        _locations = new NativeMenuItem();
        _show = new NativeMenuItem();
        _exit = new NativeMenuItem();
        _statusItem = new NativeMenuItem { IsEnabled = false };
        _toggle = new NativeMenuItem();
        _domainsItem = new NativeMenuItem();

        _fastest.Click += async (_, _) => await _coordinator.ConnectAsync(null, true);
        _toggle.Click += async (_, _) => await OnToggle(_store);

        _locations.Menu = BuildFavoritesMenu(favoriteCities, out _noFavorites);

        _domainsItem.Click += (_, _) => ShowDomains(_window);

        _show.Click += (_, _) => { _window.Show(); _window.Activate(); };

        _exit.Click += (_, _) => (Application.Current!.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.Shutdown();

        var menu = new NativeMenu();
        menu.Add(_statusItem);          // статус вверху
        menu.Add(_show);                // «Показать окно» — первое действие, до выбора локации
        menu.Add(_fastest);
        menu.Add(_locations);
        menu.Add(_domainsItem);         // кликабельно → окно на вкладке «Домены»
        menu.Add(_toggle);              // Отключить/Подключить — внизу, перед выходом
        menu.Add(new NativeMenuItemSeparator());  // единственная отбивка — у выхода
        menu.Add(_exit);
        icon.Menu = menu;

        icon.Clicked += (_, _) => { _window.Show(); _window.Activate(); };

        // Поле заполняем ДО регистрации в приложении: если SetIcons сорвётся, _icon обязан
        // указывать на эту, живую иконку, а не на уже снятую — иначе трей застынет навсегда.
        _icon = icon;
        ApplyLabels();
        Apply(_store.Current);
        return icon;
    }

    // Поиск соединения — параметром: в headless настоящей StatusNotifier-реализации нет,
    // и без этого шва пересоздание всегда уходило бы в ранний выход, то есть не проверялось бы
    // вовсе (а ломается оно как раз в сборке меню — NativeMenuItem нельзя переиспользовать).
    internal async Task RebuildAsync(Func<TrayIcon, IDisposable?> connectionLookup)
    {
        // Без соединения пересоздавать нельзя: панель убирает индикатор по исчезновению
        // владельца его bus name, и не закрыв соединение мы получим не одну лишнюю иконку,
        // а ещё одну сверху. Соединения нет — значит трей не через StatusNotifier (или
        // версия Avalonia другая), и обход просто не применяется.
        var connection = connectionLookup(_icon);
        if (connection is null) return;

        // Избранное читаем с диска и разбираем ДО снятия старой иконки и вне catch ниже.
        // Внутри снятия это была бы единственная часть сборки, способная сорваться не по вине
        // D-Bus, — и сорваться до того, как _icon получит новую иконку, то есть оставить трей
        // вообще без иконки. Здесь же сбой безобиден: старая иконка цела и работает, а
        // пересоздание повторится по следующему сигналу watcher'а (исключение погасит
        // TrayRestartPolicy).
        var cities = FavoriteCities();

        try
        {
            // Снятие идёт через TrayIconGuard: без него отмена из петли наблюдения
            // DBusTrayIconImpl.WatchAsync прилетает на диспетчер и роняет процесс.
            // Порядок внутри SwapAsync: Dispose() старой иконки (он шлёт ReleaseName по ещё
            // живому соединению), затем закрытие соединения — оно и уносит «призрачную»
            // запись расширения, — и только потом сборка новой иконки на новом соединении.
            await TrayIconGuard.SwapAsync(
                Application.Current!, _icon, () => CreateIcon(cities), connection.Dispose);
        }
        catch
        {
            // Под catch остался ровно тот чужой код, ради которого он и заведён: снятие иконки,
            // закрытие D-Bus-соединения и создание платформенной иконки заново. Сорваться там
            // может только платформа, ронять из-за этого приложение незачем — в _icon либо уже
            // новая иконка, либо прежняя, и следующий сигнал watcher'а попробует снова.
            // Подписи (ApplyLabels/Apply) сюда тоже попадают, но своего риска не добавляют: те
            // же вызовы Localizer'а идут на каждый снимок стора вообще без всякого catch.
        }
    }

    // Города избранного с диска. Читается перед каждой сборкой иконки, то есть при каждом
    // пересоздании трея: отмеченные звёздочкой локации, добавленные после старта, появляются
    // в меню после первого же пересоздания либо после перезапуска — приемлемо для MVP.
    private List<string> FavoriteCities()
    {
        var cities = new List<string>();
        foreach (var key in _favorites.Load())
        {
            // Key = "ISO|City" — берём город после разделителя.
            var sep = key.IndexOf('|');
            cities.Add(sep >= 0 ? key[(sep + 1)..] : key);
        }
        return cities;
    }

    private NativeMenu BuildFavoritesMenu(IReadOnlyList<string> cities, out NativeMenuItem? placeholder)
    {
        var submenu = new NativeMenu();
        if (cities.Count == 0)
        {
            // Подпись-заглушка локализуется вместе с остальными статичными пунктами.
            placeholder = new NativeMenuItem { IsEnabled = false };
            submenu.Add(placeholder);
            return submenu;
        }

        placeholder = null;
        foreach (var city in cities)
        {
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
            if (s.Connection == ConnectionState.Error && s.Error is { } err && UiText.Of(err).Text is { Length: > 0 } text)
                _icon.ToolTipText += "\n" + loc.Format(LocKeys.Tray_Tooltip_ErrorFormat, text);
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
