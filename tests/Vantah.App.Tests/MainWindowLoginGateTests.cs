using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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
}
