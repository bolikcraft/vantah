using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Vantah.App.ViewModels;
using Vantah.Core.Update;

namespace Vantah.App.Services;

/// <summary>
/// Проверка обновлений adguardvpn-cli, пока приложение работает: сразу при старте, затем каждый
/// час. Тумблер общий с обновлениями Vantah — за ним AppUpdateService.Enabled. Сеть чаще раза в
/// час не ходим: 24-часовой кулдаун AppUpdateService про лимиты GitHub, а не про check-update.
/// </summary>
public sealed class CliUpdateMonitor(
    IUpdateChecker checker, CliUpdateBannerViewModel banner,
    AppUpdateService appUpdates, CliUpdateStore store, TimeProvider? time = null)
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>Запустить цикл фоном. Вызывающий не ждёт ни CLI, ни сети.</summary>
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
        // Тумблер читаем на каждом тике: пользователь мог переключить его после старта.
        if (!appUpdates.Enabled) return;

        UpdateStatus status;
        try { status = await checker.CheckAsync(ct); }
        catch { return; /* сбой одной проверки цикл не останавливает: через час попробуем снова */ }

        // Вьюмодель меняем на UI-потоке: там жмут крестик и кнопку обновления.
        Dispatcher.UIThread.Post(() => Apply(status));
    }

    private void Apply(UpdateStatus status)
    {
        try
        {
            if (status.IsLatest)
            {
                // Обновление поставили в настройках или руками — плашка больше не нужна.
                banner.Hide();
                return;
            }
            // Парсер мог не найти версию в выводе CLI — показываем плашку без неё.
            if (status.LatestVersion is null
                || store.Load().DismissedVersion != status.LatestVersion)
                banner.Show(status.LatestVersion);
        }
        catch { /* исключение в Post уронило бы UI-поток */ }
    }
}
