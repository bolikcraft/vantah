using Vantah.Core.Cli;

namespace Vantah.Core.Tests.Fakes;

public sealed class FakeCliRunner : ICliRunner
{
    private readonly Queue<CliResult> _responses = new();
    public List<string[]> Calls { get; } = new();

    public FakeCliRunner Enqueue(CliResult r) { _responses.Enqueue(r); return this; }
    public FakeCliRunner Enqueue(string stdout, int exit = 0) => Enqueue(new CliResult(exit, stdout, ""));

    public Task<CliResult> RunAsync(string[] args, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        Calls.Add(args);
        return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : new CliResult(0, "", ""));
    }
}
