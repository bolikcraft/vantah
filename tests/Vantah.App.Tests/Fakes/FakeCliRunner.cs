using Vantah.Core.Cli;

namespace Vantah.App.Tests.Fakes;

/// <summary>
/// Раннер-обманка: настоящий adguardvpn-cli в тестах не зовём. По умолчанию (без Enqueue) любая
/// команда «падает» с пустым выводом — сервисы поверх него отдают пустые/ошибочные результаты,
/// и этого достаточно там, где проверяется не сам CLI, а разметка. Там, где нужен конкретный
/// сценарий ответов CLI по порядку — используй Enqueue.
/// </summary>
public sealed class FakeCliRunner : ICliRunner
{
    private readonly Queue<CliResult> _responses = new();

    /// <summary>Аргументы каждого вызова по порядку.</summary>
    public List<string[]> Calls { get; } = [];

    public FakeCliRunner Enqueue(CliResult r) { _responses.Enqueue(r); return this; }
    public FakeCliRunner Enqueue(string stdout) => Enqueue(new CliResult(0, stdout, ""));

    public Task<CliResult> RunAsync(string[] args, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        Calls.Add(args);
        return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : new CliResult(1, "", ""));
    }
}
