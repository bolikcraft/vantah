using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Vantah.App.Tray;

/// <summary>
/// Снятие иконки трея на Linux без падения процесса.
///
/// Проблема (Avalonia 12.1.0, Avalonia.FreeDesktop.DBusTrayIconImpl): иконка держит петлю
/// <c>async void WatchAsync()</c>, которая висит на <c>await Task.Delay(Timeout.Infinite, token)</c>.
/// У этого await два фильтра исключений — внутренний
/// <c>catch (OperationCanceledException) when (!_watchCts.IsCancellationRequested)</c> и внешний
/// <c>catch (Exception) when (!_isDisposed)</c>. В <c>Dispose()</c> сначала идёт
/// <c>_watchCts.Cancel()</c>, потом <c>_isDisposed = true</c>, так что к моменту, когда
/// продолжение await доезжает до UI-потока, ОБА фильтра дают false. Исключение вылетает из
/// async void, <c>Task.ThrowAsync</c> постит его на диспетчер Avalonia, и процесс умирает с
/// «Unhandled exception. System.Threading.Tasks.TaskCanceledException». Ровно этим путём
/// приложение падало и на обычном выходе через трей (rc=134 вместо 0), а не только при
/// пересоздании иконки.
///
/// Лечение двухслойное, каждый слой самодостаточен:
///  1) <see cref="Install"/> — сеть на публичном <c>Dispatcher.UIThread.UnhandledException</c>,
///     которая помечает обработанным ровно это исключение. Без рефлексии, работает всегда,
///     в том числе на выходе, где снятием иконок занимается сама Avalonia.
///  2) <see cref="SwapAsync"/> перед снятием иконки гасит петлю сам: отменяет <c>_watchCts</c>,
///     пока <c>_isDisposed</c> ещё false. Тогда внешний фильтр <c>when (!_isDisposed)</c> даёт
///     true, WatchAsync ловит отмену сам, и исключение вообще не рождается. Слой держится на
///     рефлексии по приватным полям чужой сборки, поэтому при любой осечке просто выключается.
///
/// Имена приватных членов Avalonia, на которые опирается слой 2, закреплены тестом
/// AvaloniaTrayInternalsTests: если их переименуют, тест упадёт и слой не станет тихим no-op'ом.
///
/// Проверено на ALT Linux / GNOME 4x / appindicatorsupport: три цикла «снять → создать» подряд,
/// после каждого в трее ровно одна живая иконка процесса, меню отвечает, процесс жив.
/// Без <see cref="Install"/> процесс умирает на первом же цикле.
/// </summary>
internal static class TrayIconGuard
{
    /// <summary>
    /// Диспетчер, на который уже поставлена сеть. В приложении он один на весь процесс, а вот
    /// headless-тесты поднимают свой на каждый тест, и флаг «поставлено» без привязки к диспетчеру
    /// оставлял бы сеть висеть на первом из них.
    /// </summary>
    private static Dispatcher? s_installedOn;

    /// <summary>Открытое окно снятия иконки: пока > 0, отмена из трея считается ожидаемой.</summary>
    private static int s_teardown;

    private static int s_swallowed;

    /// <summary>Сколько исключений проглотила сеть — для диагностики в живых прогонах.</summary>
    internal static int SwallowedCount => Volatile.Read(ref s_swallowed);

    /// <summary>
    /// Выключатель слоя 2 (рефлексия в приватные поля Avalonia). Если новая версия Avalonia
    /// начнёт вести себя иначе, слой можно погасить, не трогая остальной код: останется сеть.
    /// </summary>
    internal static bool UseInternalQuiesce { get; set; } = true;

    /// <summary>
    /// Ставит сеть на диспетчер. Вызывать один раз при старте приложения (до создания трея),
    /// с UI-потока. Повторные вызовы игнорируются.
    /// </summary>
    internal static void Install()
    {
        var dispatcher = Dispatcher.UIThread;
        if (ReferenceEquals(Interlocked.Exchange(ref s_installedOn, dispatcher), dispatcher)) return;

        dispatcher.UnhandledException += (_, e) =>
        {
            if (!IsExpectedTrayTeardownCancel(e.Exception)) return;

            e.Handled = true;
            Interlocked.Increment(ref s_swallowed);
        };
    }

    /// <summary>
    /// Отмена, прилетевшая из петли наблюдения снятой иконки, — единственное, что нам разрешено
    /// глушить. Два независимых признака, любого достаточно:
    ///  * стек указывает на DBusTrayIconImpl.WatchAsync — точно, но имена методов может съесть
    ///    тримминг/AOT (мы их не включаем, но публикация одним файлом это уже не гарантирует);
    ///  * открыто окно снятия иконки — грубее, зато не зависит от метаданных стека.
    /// Всё остальное уходит дальше и роняет процесс, как и должно.
    /// </summary>
    internal static bool IsExpectedTrayTeardownCancel(Exception e)
    {
        if (e is not OperationCanceledException) return false;

        var stack = e.StackTrace;
        if (stack is not null &&
            stack.Contains("DBusTrayIconImpl", StringComparison.Ordinal) &&
            stack.Contains("WatchAsync", StringComparison.Ordinal))
            return true;

        return Volatile.Read(ref s_teardown) > 0;
    }

    /// <summary>
    /// Снять текущую иконку и поставить вместо неё новую. Вызывать с UI-потока.
    /// </summary>
    /// <param name="app">Application, на котором висит коллекция TrayIcon.Icons.</param>
    /// <param name="current">Снимаемая иконка.</param>
    /// <param name="factory">Сборка новой иконки — зовётся уже после снятия старой.</param>
    /// <param name="afterDispose">
    /// Что закрыть между снятием старой иконки и созданием новой: у нас это её D-Bus-соединение.
    /// Порядок именно такой — ReleaseName при Dispose() должен уйти по ещё живому соединению,
    /// а новая иконка обязана подниматься уже на своём.
    /// </param>
    internal static async Task SwapAsync(
        Application app, TrayIcon current, Func<TrayIcon> factory, Action? afterDispose = null)
    {
        Dispatcher.UIThread.VerifyAccess();

        Interlocked.Increment(ref s_teardown);
        try
        {
            // Слой 2: гасим петлю наблюдения, пока _isDisposed ещё false.
            if (UseInternalQuiesce)
                await QuiesceWatchLoopAsync(current);

            current.Dispose();
            afterDispose?.Invoke();

            TrayIcon.SetIcons(app, new TrayIcons { factory() });

            // Даём доехать всему, что Dispose мог отправить на диспетчер, не закрывая окно:
            // пока s_teardown > 0, сеть считает отмену из трея ожидаемой.
            await SettleAsync();
        }
        finally
        {
            Interlocked.Decrement(ref s_teardown);
        }
    }

    /// <summary>
    /// Отменяет приватное поле <c>_watchCts</c> у impl-а иконки и ждёт, пока WatchAsync выйдет.
    /// Всё через рефлексию и потому строго best-effort: любая осечка — молча выходим,
    /// падение всё равно перехватит сеть из <see cref="Install"/>.
    /// </summary>
    private static async Task QuiesceWatchLoopAsync(TrayIcon icon)
    {
        try
        {
            var implProp = typeof(TrayIcon).GetProperty("Impl", BindingFlags.NonPublic | BindingFlags.Instance);
            var impl = implProp?.GetValue(icon);
            if (impl is null) return;

            // Так ведёт себя только DBus-трей; на других реализациях не трогаем ничего.
            if (impl.GetType().Name != "DBusTrayIconImpl") return;

            var ctsField = impl.GetType().GetField("_watchCts", BindingFlags.NonPublic | BindingFlags.Instance);
            if (ctsField?.GetValue(impl) is not CancellationTokenSource cts || cts.IsCancellationRequested)
                return;

            // Важно: отменяем ДО Dispose(), пока _isDisposed == false. Тогда внешний
            // catch (Exception) when (!_isDisposed) внутри WatchAsync ловит отмену сам.
            // Подменять _watchCts свежим CTS нельзя: осиротевшая петля переживёт снятие иконки
            // и разбудится на следующей смене владельца watcher'а — уже без нашего окна.
            cts.Cancel();

            // Продолжение await-а возвращается на UI-поток через AvaloniaSynchronizationContext,
            // поэтому его надо дождаться, отдав диспетчеру несколько тактов.
            await SettleAsync();
        }
        catch
        {
            // Рефлексия по приватным полям чужой сборки: поменяется версия Avalonia — просто
            // перестанем это делать. Сеть на диспетчере продолжит держать оборону.
        }
    }

    // Продолжение await-а из WatchAsync возвращается на UI-поток отдельным сообщением
    // диспетчера, а само пробуждение приходит с потока D-Bus, поэтому нужны оба ожидания:
    // такты диспетчера и короткая пауза. Всё это — внутри окна снятия, где работает сеть.
    private static async Task SettleAsync()
    {
        for (var i = 0; i < 10; i++)
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await Task.Delay(SettlePause);
    }

    private static readonly TimeSpan SettlePause = TimeSpan.FromMilliseconds(250);
}
