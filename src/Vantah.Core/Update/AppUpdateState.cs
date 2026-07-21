namespace Vantah.Core.Update;

/// <summary>Персистентное состояние проверки обновлений самого Vantah.</summary>
public sealed record AppUpdateState
{
    /// <summary>Проверка включена. По умолчанию — да.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Момент последней УСПЕШНОЙ проверки; null — не проверяли ни разу.</summary>
    public DateTimeOffset? LastCheckUtc { get; init; }

    /// <summary>Тег версии, которую пользователь скрыл крестиком.</summary>
    public string? DismissedVersion { get; init; }
}
