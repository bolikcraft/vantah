using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vantah.App.Tray;

namespace Vantah.App.Tests.Tray;

/// <summary>
/// Сеть на диспетчере из <see cref="TrayIconGuard"/> — самое опасное место обхода дубля трея:
/// она помечает исключение обработанным, то есть в теории умеет спрятать любой сбой приложения.
/// Поэтому фильтр проверяется здесь с обеих сторон: что ожидаемую отмену из петли наблюдения
/// снятой иконки он гасит и что всё остальное пропускает дальше.
///
/// Проверка идёт через настоящий Dispatcher.UIThread, а не через приватный предикат: заодно
/// доказывается, что Install() действительно подписан и что непроглоченное исключение
/// по-прежнему вылетает из диспетчера (в приложении — падением процесса).
/// </summary>
public class TrayIconGuardTests
{
    /// <summary>
    /// Имена типа и метода — единственный точный признак «отмена из петли наблюдения иконки»,
    /// который есть у сети. Настоящий DBusTrayIconImpl тут не поднять (нужна сессионная шина и
    /// живой трей), поэтому нужный стек делает одноимённая заглушка: фильтр смотрит на текст
    /// стека, и такой кадр для него неотличим от настоящего.
    /// </summary>
    private static class DBusTrayIconImpl
    {
        public static void WatchAsync() => throw new TaskCanceledException("watch loop");
    }

    /// <summary>Обычная отмена откуда-то ещё: в стеке нет ни трея, ни петли наблюдения.</summary>
    private static void CancelSomewhereElse() => throw new OperationCanceledException("elsewhere");

    /// <summary>Так падает обычная ошибка приложения — прятать её нельзя ни при каких условиях.</summary>
    private static void FailInsideTheTray() => throw new InvalidOperationException("boom");

    // Диспетчер сам вызывает подписчиков UnhandledException, а непроглоченное исключение
    // выбрасывает из RunJobs — на этом и построены проверки.
    private static Exception? RunOnDispatcher(Action action)
    {
        TrayIconGuard.Install();
        Dispatcher.UIThread.Post(action);
        try
        {
            Dispatcher.UIThread.RunJobs();
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    /// <summary>
    /// Тип исключения — первое, что смотрит фильтр. Стек здесь нарочно «правильный»: даже
    /// прилетев ровно оттуда, откуда мы ждём отмену, обычная ошибка обязана уронить процесс.
    /// </summary>
    [AvaloniaFact]
    public void A_failure_that_is_not_a_cancellation_is_never_swallowed()
    {
        var swallowed = TrayIconGuard.SwallowedCount;

        var escaped = RunOnDispatcher(() =>
        {
            try { DBusTrayIconImpl.WatchAsync(); }
            catch (OperationCanceledException) { FailInsideTheTray(); }
        });

        Assert.IsType<InvalidOperationException>(escaped);
        Assert.Equal(swallowed, TrayIconGuard.SwallowedCount);
    }

    /// <summary>
    /// Отмена, не связанная с треем: в стеке нет DBusTrayIconImpl.WatchAsync, окно снятия
    /// иконки закрыто. Это уже не «наш» случай — глотать её значит прятать чужие сбои
    /// (сорванный await любой фоновой задачи, доехавший до диспетчера).
    /// </summary>
    [AvaloniaFact]
    public void A_cancellation_from_outside_the_tray_is_not_swallowed()
    {
        var swallowed = TrayIconGuard.SwallowedCount;

        var escaped = RunOnDispatcher(CancelSomewhereElse);

        Assert.IsType<OperationCanceledException>(escaped);
        Assert.Equal(swallowed, TrayIconGuard.SwallowedCount);
    }

    /// <summary>
    /// А вот это — ровно тот случай, ради которого сеть и заведена: TaskCanceledException из
    /// петли наблюдения снятой иконки. Без неё процесс умирает с «Unhandled exception» и на
    /// пересоздании трея, и на обычном выходе через меню (rc=134).
    /// </summary>
    [AvaloniaFact]
    public void A_cancellation_from_the_tray_watch_loop_is_swallowed()
    {
        var swallowed = TrayIconGuard.SwallowedCount;

        var escaped = RunOnDispatcher(DBusTrayIconImpl.WatchAsync);

        Assert.Null(escaped);
        Assert.Equal(swallowed + 1, TrayIconGuard.SwallowedCount);
    }

    /// <summary>
    /// Второй признак — открытое окно снятия иконки: пока идёт SwapAsync, отмена считается
    /// ожидаемой и без опознаваемого стека (его может съесть тримминг/AOT). Тот же экземпляр
    /// исключения, что вне окна уходит наружу (см. тест выше), внутри окна признаётся своим —
    /// разница только в моменте.
    ///
    /// Проверяется на самом признаке, а не через диспетчер: момент, когда постнутое исключение
    /// доедет до UI-потока, headless-сессия не гарантирует, и такой тест ловил бы тайминг, а не
    /// правило. Сквозную работу сети держит тест петли наблюдения — там признак другой (стек).
    /// </summary>
    [AvaloniaFact]
    public async Task A_cancellation_during_the_swap_is_expected()
    {
        var outside = Record.Exception(CancelSomewhereElse)!;
        Assert.False(TrayIconGuard.IsExpectedTrayTeardownCancel(outside));

        bool insideWindow = false;
        await SwapWithoutQuiesceAsync(
            new TrayIcon(),
            () =>
            {
                insideWindow = TrayIconGuard.IsExpectedTrayTeardownCancel(outside);
                return new TrayIcon();
            });

        Assert.True(insideWindow);

        // Окно закрылось вместе со снятием — признак снова не срабатывает.
        Assert.False(TrayIconGuard.IsExpectedTrayTeardownCancel(outside));
    }

    /// <summary>
    /// Обратная сторона того же признака: открытое окно снятия не должно превращать сеть в
    /// «глотаем всё» — ошибка приложения обязана вылететь и внутри окна.
    /// </summary>
    [AvaloniaFact]
    public async Task A_failure_during_the_swap_is_still_not_expected()
    {
        var failure = Record.Exception(FailInsideTheTray)!;

        bool insideWindow = true;
        await SwapWithoutQuiesceAsync(
            new TrayIcon(),
            () =>
            {
                insideWindow = TrayIconGuard.IsExpectedTrayTeardownCancel(failure);
                return new TrayIcon();
            });

        Assert.False(insideWindow);
    }

    /// <summary>
    /// Порядок шагов снятия — сам обход и есть: сначала Dispose() старой иконки (ReleaseName
    /// должен уйти по ещё живому соединению), потом закрытие соединения (оно уносит
    /// «призрачную» запись расширения), потом сборка новой иконки — уже на своём соединении, —
    /// и только в конце регистрация в приложении. Перепутать любые два шага значит вернуть
    /// вторую иконку в панель.
    ///
    /// Наблюдаемы три шага из четырёх: TrayIcon.Dispose() — это <c>_impl?.Dispose()</c>, а в
    /// headless платформенной реализации трея нет вовсе (Impl == null), так что снятие старой
    /// иконки следов не оставляет. Его место в цепочке держит соседний шаг: к моменту
    /// afterDispose и сборки новой иконки в приложении ещё зарегистрирована старая.
    /// </summary>
    [AvaloniaFact]
    public async Task Swap_closes_the_connection_before_building_the_new_icon_and_registers_it_last()
    {
        var app = Application.Current!;
        var old = new TrayIcon();
        TrayIcon.SetIcons(app, new TrayIcons { old });

        var steps = new List<string>();
        var fresh = new TrayIcon();

        await SwapWithoutQuiesceAsync(
            old,
            () => { steps.Add($"factory:{Registered()}"); return fresh; },
            () => steps.Add($"afterDispose:{Registered()}"));

        Assert.Equal(new[] { "afterDispose:old", "factory:old" }, steps);
        Assert.Equal("fresh", Registered());

        string Registered()
        {
            var icons = TrayIcon.GetIcons(app);
            if (icons is null || icons.Count != 1) return $"?{icons?.Count}";
            return ReferenceEquals(icons[0], fresh) ? "fresh" : ReferenceEquals(icons[0], old) ? "old" : "other";
        }
    }

    // Слой 2 (рефлексия в приватные поля Avalonia) в headless всё равно выходит сразу: Impl у
    // иконки нет. Выключаем его явно, чтобы тест проверял ровно ту ветку, которую называет,
    // и возвращаем выключатель на место — он статический и переживает тест.
    private static async Task SwapWithoutQuiesceAsync(
        TrayIcon current, Func<TrayIcon> factory, Action? afterDispose = null)
    {
        var quiesce = TrayIconGuard.UseInternalQuiesce;
        TrayIconGuard.UseInternalQuiesce = false;
        try
        {
            await TrayIconGuard.SwapAsync(Application.Current!, current, factory, afterDispose);
        }
        finally
        {
            TrayIconGuard.UseInternalQuiesce = quiesce;
        }
    }
}
