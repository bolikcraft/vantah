using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Vantah.App;

/// <summary>
/// Маршалинг мутаций привязанного состояния на UI-поток. Если уже на UI-потоке — выполняем
/// inline (InvokeAsync безусловно ставит колбэк в очередь диспетчера даже с UI-потока, что
/// ломало бы синхронные тесты и без нужды откладывало апдейт); иначе — маршалим через InvokeAsync.
/// Нужен потому, что продолжение после await в реальном CliRunner оказывается на потоке пула,
/// а правка привязанных ObservableCollection/свойств вне UI-потока роняет Avalonia cross-thread.
/// </summary>
public static class UiThread
{
    public static Task RunAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) { action(); return Task.CompletedTask; }
        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }

    /// <summary>
    /// То же для асинхронной работы, которую надо начать (и продолжать) на UI-потоке:
    /// перегрузка InvokeAsync(Func&lt;Task&gt;) уже разворачивает внутреннюю задачу, поэтому
    /// ожидание результата — это ожидание всей работы, а не только её запуска.
    /// </summary>
    public static Task RunAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess()) return action();
        return Dispatcher.UIThread.InvokeAsync(action);
    }
}
