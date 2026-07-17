using Vantah.Core.Cli;

namespace Vantah.Core.Update;

/// <summary>Проверяет обновление CLI через <c>check-update</c>. Установку НЕ делает.</summary>
public sealed class UpdateChecker(ICliRunner cli) : IUpdateChecker
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<UpdateStatus> CheckAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["check-update"], Timeout, ct);
        // Сбой проверки (часто — сеть) не должен рождать ложный алерт: считаем «актуально».
        if (!r.Ok) return new UpdateStatus(IsLatest: true, LatestVersion: null);
        return UpdateCheckParser.Parse(r.Stdout);
    }
}
