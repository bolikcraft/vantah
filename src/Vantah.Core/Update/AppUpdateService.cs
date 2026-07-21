namespace Vantah.Core.Update;

/// <summary>
/// Решает, показывать ли плашку об обновлении Vantah. Вся политика здесь: тумблер, кулдаун,
/// сравнение версий, скрытая пользователем версия. Момент времени передаётся параметром —
/// так политика проверяется тестами без ожиданий и без сети.
/// </summary>
public sealed class AppUpdateService(IAppReleaseSource source, AppUpdateStore store, string currentVersion)
{
    /// <summary>Минимальный промежуток между обращениями к GitHub.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    /// <summary>Тумблер «Проверять обновления Vantah» из настроек.</summary>
    public bool Enabled
    {
        get => store.Load().Enabled;
        set => store.Save(store.Load() with { Enabled = value });
    }

    public async Task<AppUpdateInfo?> CheckAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var state = store.Load();
        if (!state.Enabled) return null;
        if (state.LastCheckUtc is { } last && now - last < CheckInterval) return null;

        var release = await source.GetLatestAsync(ct);
        // Неудачную проверку временем не отмечаем: иначе один старт без сети заглушил бы
        // проверку на сутки. При следующем запуске попробуем снова.
        if (release is null) return null;
        store.Save(state with { LastCheckUtc = now });

        if (string.Equals(release.Version, state.DismissedVersion, StringComparison.Ordinal)) return null;

        var latest = ReleaseTagParser.Parse(release.Version);
        var current = ReleaseTagParser.Parse(currentVersion);
        if (latest is null || current is null || latest <= current) return null;

        return release;
    }

    /// <summary>Скрыть эту версию навсегда — крестик на плашке.</summary>
    public void Dismiss(string version) =>
        store.Save(store.Load() with { DismissedVersion = version });
}
