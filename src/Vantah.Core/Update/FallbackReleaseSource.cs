namespace Vantah.Core.Update;

/// <summary>
/// Спрашивает основной источник, а когда тот промолчал (сеть, 403 по лимиту API, битый ответ) —
/// запасной. Молчание обоих означает, что проверка не состоялась: показывать нечего.
/// </summary>
public sealed class FallbackReleaseSource(IAppReleaseSource primary, IAppReleaseSource fallback) : IAppReleaseSource
{
    public async Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default) =>
        await primary.GetLatestAsync(ct) ?? await fallback.GetLatestAsync(ct);
}
