using Vantah.Core.Cli;

namespace Vantah.Core.Tests.Fakes;

/// <summary>Детерминированный дубль: заранее заданная очередь порций вывода; записывает всё, что «ввели».</summary>
public sealed class FakeInteractiveSession : IInteractiveCliSession
{
    private readonly Queue<string?> _output;
    public List<string> Written { get; } = new();       // строки (пароль тут — только в тестах, фиктивный)
    public int ExitCode { get; set; }

    public FakeInteractiveSession(params string?[] output) => _output = new(output);

    public Task<string?> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();   // как реальная сессия — отмена прерывает чтение
        return Task.FromResult(_output.Count > 0 ? _output.Dequeue() : null);
    }

    public Task WriteLineAsync(string line, CancellationToken ct = default) { Written.Add(line); return Task.CompletedTask; }
    public Task WriteLineAsync(char[] chars, CancellationToken ct = default) { Written.Add(new string(chars)); return Task.CompletedTask; }
    public Task<int> WaitForExitAsync(CancellationToken ct = default) => Task.FromResult(ExitCode);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Дубль раннера: отдаёт заранее подготовленную сессию, запоминает аргументы.</summary>
public sealed class FakeInteractiveRunner : IInteractiveCliRunner
{
    private readonly FakeInteractiveSession _session;
    public string[]? StartedArgs { get; private set; }
    public FakeInteractiveRunner(FakeInteractiveSession session) => _session = session;
    public Task<IInteractiveCliSession> StartAsync(string[] args, CancellationToken ct = default)
    {
        StartedArgs = args;
        return Task.FromResult<IInteractiveCliSession>(_session);
    }
}
