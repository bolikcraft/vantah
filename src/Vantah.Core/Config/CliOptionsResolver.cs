namespace Vantah.Core.Config;

/// <summary>
/// Собирает <see cref="CliOptions" /> по приоритету: INI-конфиг → переменная окружения → дефолт.
/// Чистая функция: ни файлов, ни реального окружения — источники передаются извне.
/// </summary>
public static class CliOptionsResolver
{
    /// <summary>Ключ INI с путём к исполняемому файлу CLI.</summary>
    public const string ExecutableKey = "adguard_cmd";

    /// <summary>Ключ INI с командой принудительного завершения процесса.</summary>
    public const string KillCommandKey = "kill_cmd";

    /// <summary>Переменная окружения с путём к исполняемому файлу CLI.</summary>
    public const string ExecutableEnvVar = "VANTAH_ADGUARD_CMD";

    /// <summary>Переменная окружения с командой принудительного завершения процесса.</summary>
    public const string KillCommandEnvVar = "VANTAH_KILL_CMD";

    /// <summary>Исполняемый файл по умолчанию — ищется в PATH.</summary>
    public const string DefaultExecutable = "adguardvpn-cli";

    /// <param name="config">Разобранный INI (пустой, если файла нет).</param>
    /// <param name="env">Чтение переменной окружения по имени, обычно Environment.GetEnvironmentVariable.</param>
    /// <param name="defaultExecutable">Чем исполнять CLI, когда не задано ни в конфиге, ни в окружении.</param>
    public static CliOptions Resolve(
        IniConfig config,
        Func<string, string?> env,
        string defaultExecutable = DefaultExecutable)
    {
        // Значение из одних пробелов трактуем как незаданное: иначе пустая строка в конфиге
        // молча ломала бы запуск вместо того, чтобы откатиться к дефолту.
        var executable = FirstSet(config.Get(ExecutableKey), env(ExecutableEnvVar)) ?? defaultExecutable;
        var killCommand = FirstSet(config.Get(KillCommandKey), env(KillCommandEnvVar));

        return new CliOptions(executable, killCommand);
    }

    private static string? FirstSet(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim();
}
