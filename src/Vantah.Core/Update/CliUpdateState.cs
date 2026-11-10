namespace Vantah.Core.Update;

/// <summary>Персистентное состояние проверки обновлений adguardvpn-cli.</summary>
public sealed record CliUpdateState
{
    /// <summary>Версия CLI, скрытая крестиком на плашке.</summary>
    public string? DismissedVersion { get; init; }
}
