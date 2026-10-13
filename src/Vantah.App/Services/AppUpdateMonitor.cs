using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Vantah.App.ViewModels;
using Vantah.Core.Update;

namespace Vantah.App.Services;

/// <summary>
/// Проверка обновлений Vantah, пока приложение работает: сразу при старте, затем каждый час.
/// Vantah неделями живёт в трее, и проверка только при старте не заметила бы новый релиз.
/// В сеть чаще раза в сутки не ходим — кулдаун держит <see cref="AppUpdateService"/>.
/// </summary>
public sealed class AppUpdateMonitor(
    AppUpdateService updates, UpdateBannerViewModel banner, AboutViewModel about, TimeProvider? time = null)
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>
    /// Запустить цикл фоном. Вызывающий не ждёт ни файла состояния, ни сети.
    /// Задача завершается только по отмене.
    /// </summary>
    public Task Start(CancellationToken ct = default) => Task.Run(() => RunAsync(ct), CancellationToken.None);

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await CheckOnceAsync(ct);
            try { await Task.Delay(TickInterval, _time, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task CheckOnceAsync(CancellationToken ct)
    {
        AppUpdateInfo? info;
        try { info = await updates.CheckAsync(_time.GetUtcNow(), ct); }
        catch { return; /* сбой одной проверки цикл не останавливает: через час попробуем снова */ }

        // Привязанные свойства, заполненные из фонового потока, в UI не доезжают.
        Dispatcher.UIThread.Post(() => Apply(info));
    }

    private void Apply(AppUpdateInfo? info)
    {
        try
        {
            about.ShowUpdate(info);
            // Скрытие проверяем на UI-потоке: крестик жмут здесь же, и проверка с ним не разойдётся.
            // На null плашку не прячем: её закрывает только пользователь.
            if (info is not null && !updates.IsDismissed(info)) banner.Show(info);
        }
        catch { /* исключение в Post уронило бы UI-поток */ }
    }
}
