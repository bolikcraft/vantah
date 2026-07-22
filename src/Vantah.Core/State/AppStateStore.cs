using Vantah.Core.Errors;
using Vantah.Core.Models;

namespace Vantah.Core.State;

public sealed record AppSnapshot
{
    // До первого опроса CLI реальный статус неизвестен: показывать «Отключено» на старте нельзя.
    public ConnectionState Connection { get; init; } = ConnectionState.Unknown;
    public string? Location { get; init; }
    public string? Mode { get; init; }
    public string? Interface { get; init; }
    public TrafficSample? Traffic { get; init; }
    public AppError? Error { get; init; }   // код причины: текст собирает UI на своём языке
    public int ExclusionsCount { get; init; }
    public string? LocationDisplay { get; init; }              // «Amsterdam, Netherlands» (человекочитаемо)
    public SiteExclusionMode ExclusionsMode { get; init; }     // текущий режим исключений
    public LoginState LoginState { get; init; } = LoginState.Unknown;
}

public sealed class AppStateStore
{
    private readonly object _gate = new();
    private volatile AppSnapshot _current = new();
    public AppSnapshot Current => _current;
    public event EventHandler<AppSnapshot>? Changed;

    // Set рассчитан на единственного фонового писателя; мутация выполняется под _gate,
    // а событие Changed поднимается уже вне блокировки.
    public void Set(Func<AppSnapshot, AppSnapshot> mutate)
    {
        AppSnapshot next;
        lock (_gate)
        {
            next = mutate(_current);
            _current = next;
        }
        Changed?.Invoke(this, next);
    }
}
