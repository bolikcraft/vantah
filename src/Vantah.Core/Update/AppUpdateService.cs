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
    /// в остальное время отвечает сохранённым релизом. Скрытую крестиком версию НЕ отсеивает.
    /// </summary>
    public async Task<AppUpdateInfo?> CheckAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var state = store.Load();
        if (!state.Enabled) return null;

        var release = state.LatestRelease;
        if (state.LastCheckUtc is not { } last || now - last >= CheckInterval)
        {
            // Неудачную проверку временем не отмечаем: иначе один старт без сети заглушил бы
            // проверку на сутки. Пока сети нет, отвечаем тем, что нашли раньше.
            if (await source.GetLatestAsync(ct) is { } fetched)
            {
                // Не снимок, снятый ДО запроса: запрос длится до 10 секунд, и за это время пользователь
                // мог снять галку или нажать крестик — запись снимка вернула бы их назад.
                store.Update(s => s with { LastCheckUtc = now, LatestRelease = fetched });
                release = fetched;
            }
        }

        return IsNewer(release) ? release : null;
    }

    /// <summary>Пользователь скрыл эту версию крестиком на плашке.</summary>
    public bool IsDismissed(AppUpdateInfo info) =>
        string.Equals(info.Version, store.Load().DismissedVersion, StringComparison.Ordinal);

    /// <summary>Скрыть эту версию навсегда — крестик на плашке.</summary>
    public void Dismiss(string version) =>
        store.Update(s => s with { DismissedVersion = version });

    private bool IsNewer(AppUpdateInfo? release)
    {
        var latest = ReleaseTagParser.Parse(release?.Version);
        var current = ReleaseTagParser.Parse(currentVersion);
        return latest is not null && current is not null && latest > current;
    }
}
