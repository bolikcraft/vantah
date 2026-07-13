using Vantah.Core.Cli;

namespace Vantah.App.Tests.Fakes;

/// <summary>
/// Раннер-обманка: настоящий adguardvpn-cli в тестах не зовём. Любая команда «падает» с пустым
/// выводом — сервисы поверх него отдают пустые/ошибочные результаты, и этого достаточно там,
/// где проверяется не сам CLI, а разметка.
/// </summary>
public sealed class FakeCliRunner : ICliRunner
{
    /// <summary>Аргументы каждого вызова по порядку.</summary>
    public List<string[]> Calls { get; } = [];

    public Task<CliResult> RunAsync(string[] args, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        Calls.Add(args);
        return Task.FromResult(new CliResult(1, "", ""));
    }
}
