using Vantah.Core.Models;

namespace Vantah.Core.State;

public sealed record AppSnapshot
{
    public ConnectionState Connection { get; init; } = ConnectionState.Disconnected;
    public string? Location { get; init; }
    public string? Mode { get; init; }
    public string? Interface { get; init; }
    public TrafficSample? Traffic { get; init; }
    public string? Error { get; init; }
}

public sealed class AppStateStore
{
    private readonly object _gate = new();
    public AppSnapshot Current { get; private set; } = new();
    public event EventHandler<AppSnapshot>? Changed;

    public void Set(Func<AppSnapshot, AppSnapshot> mutate)
    {
        AppSnapshot next;
        lock (_gate)
        {
            next = mutate(Current);
            Current = next;
        }
        Changed?.Invoke(this, next);
    }
}
