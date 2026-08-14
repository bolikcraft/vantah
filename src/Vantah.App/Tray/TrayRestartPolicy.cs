using System;
using System.Threading.Tasks;

namespace Vantah.App.Tray;

/// <summary>
/// Когда пересоздавать иконку в трее по появлению владельца org.kde.StatusNotifierWatcher.
///
/// GNOME при блокировке экрана выгружает расширение appindicatorsupport, и имя
/// org.kde.StatusNotifierWatcher уходит с сессионной шины. При возврате расширение сканирует
/// шину раньше, чем Avalonia успевает взять своё well-known имя, и регистрирует наш объект
/// /StatusNotifierItem под ключом «:1.NNN@/StatusNotifierItem»; следом Avalonia регистрирует
/// его же под «org.kde.StatusNotifierItem-pid-N». Ключи разные — в трее две иконки на один
/// объект. Лечится только пересозданием трея с закрытием старого D-Bus-соединения.
///
/// Само правило — два пункта:
/// 1) КАЖДОЕ появление владельца, пришедшее сигналом, — повод пересоздать трей. Это и возврат
///    расширения после блокировки экрана, и автозапуск, где Vantah стартует раньше расширения:
///    в обоих случаях наш StatusNotifierItem уже висит на шине к моменту скана и получает
///    вторую регистрацию. Владельца, сидевшего на шине ещё до старта наблюдения, правило не
///    видит вовсе и видеть не должно: он зарегистрировал наш индикатор один раз, дубля нет;
/// 2) пока пересоздание запланировано или идёт, новые появления не копятся: и пропажа, и
///    возврат имени могут придти пачкой сигналов, а пересоздание нужно ровно одно.
///
/// Без D-Bus класс не работает и потому тестируется отдельно от него: паузу и само
/// пересоздание он получает делегатами.
/// </summary>
internal sealed class TrayRestartPolicy
{
    private readonly Func<Task> _delay;
    private readonly Func<Task> _rebuild;
    private readonly object _gate = new();

    private bool _pending;

    /// <param name="delay">
    /// Пауза между появлением владельца и пересозданием: к её концу расширение должно доделать
    /// свой скан шины, а Avalonia — свою регистрацию, иначе пересоздание попадёт в ту же гонку.
    /// </param>
    /// <param name="rebuild">Собственно пересоздание трея.</param>
    public TrayRestartPolicy(Func<Task> delay, Func<Task> rebuild)
    {
        _delay = delay;
        _rebuild = rebuild;
    }

    /// <summary>Текущее/последнее пересоздание — чтобы его можно было дождаться в тестах.</summary>
    public Task LastRunTask { get; private set; } = Task.CompletedTask;

    /// <summary>Пришёл NameOwnerChanged: <paramref name="hasOwner"/> — есть ли теперь владелец.</summary>
    public void OwnerChanged(bool hasOwner)
    {
        lock (_gate)
        {
            // Пропажу имени просто пережидаем: иконку в этот момент и так никто не показывает,
            // а Avalonia сама снимет свою регистрацию.
            if (!hasOwner) return;

            if (_pending) return;
            _pending = true;

            // Присваивание внутри блокировки: тест, дождавшийся возврата OwnerChanged,
            // обязан видеть свежую LastRunTask, а не завершённую заглушку из инициализатора.
            LastRunTask = RunAsync();
        }
    }

    private async Task RunAsync()
    {
        try
        {
            await _delay();
            await _rebuild();
        }
        catch
        {
            // Пересоздание трея необязательно: сорваться оно может только на чужом коде
            // (D-Bus, платформенный трей), и ронять из-за этого приложение незачем.
        }
        finally
        {
            lock (_gate) _pending = false;
        }
    }
}
