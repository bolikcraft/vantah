namespace Vantah.Core.Update;

/// <summary>
/// Решает, есть ли обновление Vantah. Вся политика здесь: тумблер, кулдаун, сравнение версий,
/// скрытая пользователем версия. Момент времени передаётся параметром — так политика
/// проверяется тестами без ожиданий и без сети.
/// </summary>
public sealed class AppUpdateService(IAppReleaseSource source, AppUpdateStore store, string currentVersion)
{
    /// <summary>Минимальный промежуток между обращениями к GitHub.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    /// <summary>Тумблер «Проверять обновления Vantah» из настроек.</summary>
    public bool Enabled
    {
        get => store.Load().Enabled;
        set => store.Update(s => s with { Enabled = value });
    }

    /// <summary>
    /// Релиз новее текущей версии или null. В сеть идёт не чаще <see cref="CheckInterval"/>,
    /// пока релиз сохранён; в остальное время отвечает им. Скрытую крестиком версию НЕ отсеивает.
    /// </summary>
    public async Task<AppUpdateInfo?> CheckAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var state = store.Load();
        if (!state.Enabled) return null;

        if (CooldownExpired(state, now))
        {
            var fetched = await source.GetLatestAsync(ct);
            // Запрос длится до 10 секунд, и за это время пользователь мог выключить проверку:
            // дальше смотрим на свежее состояние, а не на снимок до запроса.
            // Неудачу временем не отмечаем, чтобы один старт без сети не заглушил проверку на сутки.
            state = fetched is null
                ? store.Load()
                : store.Update(s => s with { LastCheckUtc = now, LatestRelease = fetched });
            // Релиз сохранён и при выключенной проверке, но показывать его уже нельзя.
            if (!state.Enabled) return null;
        }

        return IsNewer(state.LatestRelease) ? state.LatestRelease : null;
    }

    /// <summary>Пользователь скрыл эту версию крестиком на плашке.</summary>
    public bool IsDismissed(AppUpdateInfo info) =>
        string.Equals(info.Version, store.Load().DismissedVersion, StringComparison.Ordinal);

    /// <summary>Скрыть эту версию навсегда — крестик на плашке.</summary>
    public void Dismiss(string version) =>
        store.Update(s => s with { DismissedVersion = version });

    // Без сохранённого релиза сутки не ждём: прежние версии писали время проверки, но не релиз.
    // Удачный запрос релиз всегда сохраняет, так что лишних запросов это не даёт.
    private static bool CooldownExpired(AppUpdateState state, DateTimeOffset now) =>
        state.LatestRelease is null
        || state.LastCheckUtc is not { } last
        || now - last >= CheckInterval;

    private bool IsNewer(AppUpdateInfo? release)
    {
        var latest = ReleaseTagParser.Parse(release?.Version);
        var current = ReleaseTagParser.Parse(currentVersion);
        return latest is not null && current is not null && latest > current;
    }
}
