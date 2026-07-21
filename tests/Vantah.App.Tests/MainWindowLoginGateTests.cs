using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Models;
using Vantah.Core.State;
using Xunit;

/// <summary>
/// Гейт по логину: когда сессии нет — видна форма входа, рабочий UI скрыт; когда есть — наоборот.
/// Зелёная сборка этого не докажет (память проекта: no-op кнопки Kill прошли сборку), поэтому
/// поднимаем окно headless и смотрим фактическую видимость и привязку команды выхода.
/// </summary>
public class MainWindowLoginGateTests
{
    private static (MainWindow Window, AppStateStore Store, FakeAuthService Auth) ShowWith(LoginState state)
    {
        var store = new AppStateStore();
        var auth = new FakeAuthService { State = state };
        var window = Vantah.App.Tests.MainWindowFactory.Build(state, store, auth);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, store, auth);
    }

    // Ищем по именам, а не по типу: в корневой сетке рядом с гейтом лежит ещё и Border плашки
    // об обновлении, и OfType<Border>().Single() на нём бы упал.
    private static Border LoginWrapper(MainWindow window) =>
        window.FindControl<Border>("LoginGate")!;

    private static Grid WorkingArea(MainWindow window) =>
        window.FindControl<Grid>("WorkingArea")!;

    [AvaloniaFact]
    public void Logged_out_shows_login_form_and_hides_working_ui()
    {
        var (window, _, _) = ShowWith(LoginState.LoggedOut);
        Assert.True(LoginWrapper(window).IsVisible);    // форма входа
        Assert.False(WorkingArea(window).IsVisible);    // вкладки/меню скрыты
    }

    [AvaloniaFact]
    public void Logged_in_shows_working_ui_and_hides_login_form()
    {
        var (window, _, _) = ShowWith(LoginState.LoggedIn);
        Assert.False(LoginWrapper(window).IsVisible);
        Assert.True(WorkingArea(window).IsVisible);
    }

    [AvaloniaFact]
    public void Unknown_state_keeps_working_ui_visible()
    {
        // При Unknown (зонд не прошёл) не мигаем формой входа зря — гейт по «!= LoggedOut».
        var (window, _, _) = ShowWith(LoginState.Unknown);
        Assert.True(WorkingArea(window).IsVisible);
        Assert.False(LoginWrapper(window).IsVisible);
    }

    [AvaloniaFact]
    public void Logout_command_calls_auth_and_flips_gate_to_login_form()
    {
        // Пункт «Выйти» дёргает LogoutCommand вьюмодели (Click-обработчик привязан на этапе
        // компиляции XAML). Проверяем, что команда реально вызывает logout и — после того как
        // зонд увидит выход — гейт показывает форму входа.
        var (window, store, auth) = ShowWith(LoginState.LoggedIn);
        var vm = (MainWindowViewModel)window.DataContext!;

        auth.State = LoginState.LoggedOut;            // после logout зонд увидит «не залогинен»
        vm.LogoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, auth.LogoutCalls);
        Assert.Equal(LoginState.LoggedOut, store.Current.LoginState);
        Assert.False(vm.IsLoggedIn);
        Assert.True(LoginWrapper(window).IsVisible);
        Assert.False(WorkingArea(window).IsVisible);
    }

    /// <summary>
    /// Выход необратим, поэтому пункт меню сначала спрашивает подтверждение: до ответа
    /// пользователя logout не вызывается, «Отмена» отменяет его совсем, а кнопка подтверждения —
    /// выполняет. Без headless-прогона это не доказать: обработчик мог бы молча разлогинивать.
    /// </summary>
    [AvaloniaFact]
    public void Logout_menu_item_asks_for_confirmation_first()
    {
        var (window, _, auth) = ShowWith(LoginState.LoggedIn);

        ClickLogout(window);
        var confirm = ConfirmWindow(window);

        Assert.Equal(0, auth.LogoutCalls);             // ничего не произошло, пока не подтвердили

        DialogButton(confirm, LocKeys.Common_Cancel).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, auth.LogoutCalls);             // «Отмена» — выхода нет
        Assert.Empty(window.OwnedWindows);             // и диалог закрылся

        ClickLogout(window);
        DialogButton(ConfirmWindow(window), LocKeys.Login_LogoutConfirmAction)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, auth.LogoutCalls);             // подтверждение доводит выход до конца
    }

    private static Window ConfirmWindow(MainWindow window) =>
        Assert.Single(window.OwnedWindows, w => w.Title == Localizer.Instance[LocKeys.Login_LogoutConfirmTitle]);

    private static Button DialogButton(Window dialog, string textKey) =>
        dialog.GetVisualDescendants().OfType<Button>()
            .Single(b => (b.Content as string) == Localizer.Instance[textKey]);

    // Клик по пункту меню — как у пользователя: раскрываем флайаут и жмём мышью (см. MainWindowTests).
    private static void ClickLogout(MainWindow window)
    {
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Flyout is MenuFlyout);
        var flyout = (MenuFlyout)button.Flyout!;
        flyout.ShowAt(button);
        Dispatcher.UIThread.RunJobs();

        var item = flyout.Items.OfType<MenuItem>()
            .Single(i => (i.Header as string) == Localizer.Instance[LocKeys.Login_Logout]);
        var center = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window);
        Assert.NotNull(center);

        window.MouseMove(center!.Value);
        window.MouseDown(center.Value, MouseButton.Left);
        window.MouseUp(center.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
