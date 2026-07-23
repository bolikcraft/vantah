using Vantah.Core.Cli;

namespace Vantah.Core.Update;

/// <summary>
/// Устанавливает обновление CLI: <c>update -y</c>. Код 17 = «уже актуально» (не ошибка,
/// тот же код, что у <c>check-update</c>), код 0 = обновлено, иначе — сбой. Команда блокирующая
/// (не форкает), при реальном обновлении качает — отсюда длинный таймаут.
/// </summary>
public sealed class CliUpdater(ICliRunner cli) : ICliUpdater
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(180);
    private const int AlreadyLatestExitCode = 17;

    public async Task<UpdateResult> UpdateAsync(CancellationToken ct = default)
    {
        var r = await cli.RunAsync(["update", "-y"], Timeout, ct);
        var output = Ansi.Strip(string.IsNullOrWhiteSpace(r.Stderr) ? r.Stdout : $"{r.Stdout}\n{r.Stderr}").Trim();
        var outcome = r.ExitCode switch
        {
            0 => UpdateOutcome.Updated,
            AlreadyLatestExitCode => UpdateOutcome.AlreadyLatest,
            _ => UpdateOutcome.Failed,
        };
        return new UpdateResult(outcome, output);
    }
}
