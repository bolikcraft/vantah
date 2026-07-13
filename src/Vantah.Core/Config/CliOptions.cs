namespace Vantah.Core.Config;

/// <summary>Как запускать CLI: чем исполнять и чем принудительно убивать зависшие процессы.</summary>
/// <param name="Executable">Исполняемый файл adguardvpn-cli: имя из PATH либо полный путь.</param>
/// <param name="KillCommand">Шаблон внешней команды убийства (например «pkexec kill») или null — тогда kill(2).</param>
public sealed record CliOptions(string Executable, string? KillCommand);
