using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Vantah.Core.Models;
using Vantah.Core.State;

namespace Vantah.App.Services;

/// <summary>
/// Разделы, которые не удалось прочитать без VPN (таймаут CLI), перечитываются сами в момент
/// успешного подключения: CLI снова отвечает, и требовать от пользователя ручного «Обновить»
/// незачем. Признак «не прочиталось» живёт в самих разделах и только в памяти процесса.
/// </summary>
public sealed class SectionReloader
{
    private readonly IReadOnlyList<IReloadableSection> _sections;
    private volatile ConnectionState _previous;
    private int _inFlight;

    // Настоящий переход в Connected, случившийся, пока уже идёт прогон. Перезагрузка разделов
    // может занять секунды (несколько вызовов CLI подряд), и такой переход — не редкость.
    // Одного отложенного повтора достаточно: копить очередь незачем, к моменту повтора
    // достаточно перечитать то, что на текущий момент всё ещё LoadFailed.
    private volatile bool _rerunRequested;

    public SectionReloader(AppStateStore store, IReadOnlyList<IReloadableSection> sections)
    {
        _sections = sections;
        _previous = store.Current.Connection;
        store.Changed += OnStoreChanged;
    }

    /// <summary>Текущий/последний прогон перезагрузки — чтобы его можно было дождаться в тестах.</summary>
    public Task LastRunTask { get; private set; } = Task.CompletedTask;

    private void OnStoreChanged(object? sender, AppSnapshot snapshot)
    {
        var previous = _previous;
        _previous = snapshot.Connection;

        // Реагируем на ПЕРЕХОД в Connected: опрос пишет Connected каждые несколько секунд,
        // и реакция на само значение перезапускала бы загрузку бесконечно.
        if (snapshot.Connection != ConnectionState.Connected || previous == ConnectionState.Connected) return;

        // Single-flight: пока прошлый прогон не закончился, новый параллельно не начинаем — но
        // и не отбрасываем переход молча. Раньше именно так и было: если реальный переход
        // Disconnected→Connected случался, пока уже шёл прогон, попытка терялась насовсем, и
        // раздел с LoadFailed мог провисеть до следующего полного цикла отключения-подключения.
        // Теперь запоминаем, что переход был, — уже запущенный прогон повторит себя сам.
        if (Interlocked.Exchange(ref _inFlight, 1) == 1)
        {
            _rerunRequested = true;
            return;
        }

        // Присваивание — синхронно, внутри обработчика Changed (т.е. ДО того как store.Set(...)
        // вернёт управление вызвавшему его коду), даже если сам обработчик выполняется на потоке
        // пула. Так тест, дождавшийся своего Task.Run(() => store.Set(...)), гарантированно видит
        // актуальную LastRunTask, а не устаревшую Task.CompletedTask из конструктора. Ожидание
        // LastRunTask покрывает и отложенный повтор — он часть того же Task, см. RunAndDrainAsync.
        LastRunTask = RunOnUiThread(RunAndDrainAsync);
    }

    // Прогоняет перезагрузку и, если во время неё подоспел ещё один настоящий переход в
    // Connected, сразу повторяет её — не выходя из-под LastRunTask. _inFlight остаётся
    // выставленным на всё время цикла: конкурентный прогон по-прежнему невозможен, отложенный
    // повтор просто выполняется в рамках уже идущего.
    private async Task RunAndDrainAsync()
    {
        try
        {
            do
            {
                _rerunRequested = false;
                await ReloadFailedAsync();
            } while (_rerunRequested);
        }
        finally
        {
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }

    private async Task ReloadFailedAsync()
    {
        // Последовательно, а не параллельно: каждый вызов — отдельный процесс CLI,
        // три сразу дают всплеск и ничего заметно не ускоряют.
        foreach (var section in _sections)
        {
            if (!section.LoadFailed) continue;
            try { await section.ReloadIfFailedAsync(); }
            catch { /* сбой показывает сам раздел: LoadError + «Обновить» */ }
        }
    }

    // Changed приходит с потока опроса, а перезагрузка правит привязанные к UI свойства —
    // стартуем её строго на UI-потоке (тот же приём, что в UiThread.RunAsync).
    private static Task RunOnUiThread(Func<Task> start) =>
        Dispatcher.UIThread.CheckAccess()
            ? start()
            : Dispatcher.UIThread.InvokeAsync(start);
}
