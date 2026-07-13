namespace Vantah.Core.Cli;

/// <summary>
/// Скан procfs: находит ВСЕ процессы CLI в системе, включая привилегированный туннель, который
/// CLI демонизирует через «sudo -b» и который нашим ребёнком не становится. Корень параметризован
/// ради тестов; формат файлов — стабильная часть ABI ядра (man 5 proc).
/// </summary>
/// <param name="executable">Путь к бинарю CLI; сравнивается имя файла (см. <see cref="ProcessCmdline"/>).</param>
public sealed class ProcFsProcessSource(string executable, string procRoot = "/proc") : IProcessSource
{
    /// <summary>USER_HZ: единица поля starttime. На Linux всегда 100, из процесса не читается.</summary>
    private const long TicksPerSecond = 100;

    /// <summary>Поле starttime — 22-е в /proc/PID/stat, т.е. 20-е (индекс 19) после «(comm)».</summary>
    private const int StartTimeIndexAfterComm = 19;

    /// <inheritdoc />
    public IReadOnlyList<RunningProcess> Scan()
    {
        var bootTime = ReadBootTime();
        if (bootTime is null) return [];

        var found = new List<RunningProcess>();
        foreach (var dir in EnumerateDirectories())
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid)) continue; // «self», «net», …

            var cmdline = ReadCmdline(dir);
            if (cmdline is null || !ProcessCmdline.Matches(cmdline, executable)) continue;

            var startedAt = ReadStartedAt(dir, bootTime.Value);
            if (startedAt is null) continue; // умер, пока мы читали его каталог

            // Id для UI = pid: реестра со своей нумерацией больше нет, а pid жив ровно столько же,
            // сколько строка в таблице.
            found.Add(new RunningProcess(pid, pid, cmdline[0], cmdline.Skip(1).ToArray(), startedAt.Value));
        }

        return found.OrderBy(p => p.StartedAt).ThenBy(p => p.Pid).ToArray();
    }

    private IEnumerable<string> EnumerateDirectories()
    {
        try { return Directory.EnumerateDirectories(procRoot); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    /// <summary>Момент загрузки машины: /proc/stat, строка «btime &lt;unixtime&gt;».</summary>
    private DateTimeOffset? ReadBootTime()
    {
        var stat = ReadText(Path.Combine(procRoot, "stat"));
        if (stat is null) return null;

        foreach (var line in stat.Split('\n'))
        {
            if (!line.StartsWith("btime ", StringComparison.Ordinal)) continue;
            if (long.TryParse(line.AsSpan(6).Trim(), out var seconds))
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

    /// <summary>Аргументы процесса: /proc/PID/cmdline, разделитель — NUL. null — процесса уже нет.</summary>
    private static string[]? ReadCmdline(string dir)
    {
        var raw = ReadText(Path.Combine(dir, "cmdline"));
        if (string.IsNullOrEmpty(raw)) return null; // пусто — ядерный поток либо зомби

        var args = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return args.Length == 0 ? null : args;
    }

    /// <summary>Старт процесса: btime + starttime (тики с загрузки) из /proc/PID/stat.</summary>
    private static DateTimeOffset? ReadStartedAt(string dir, DateTimeOffset bootTime)
    {
        var stat = ReadText(Path.Combine(dir, "stat"));
        if (stat is null) return null;

        // comm заключено в скобки и может содержать пробелы и даже ')' — режем по ПОСЛЕДНЕЙ скобке.
        var commEnd = stat.LastIndexOf(')');
        if (commEnd < 0) return null;

        var fields = stat[(commEnd + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length <= StartTimeIndexAfterComm) return null;
        if (!long.TryParse(fields[StartTimeIndexAfterComm], out var ticks)) return null;

        return bootTime.AddSeconds((double)ticks / TicksPerSecond);
    }

    /// <summary>Чтение файла procfs. null вместо исключения: процесс мог умереть между listdir и read.</summary>
    private static string? ReadText(string path)
    {
        try { return File.ReadAllText(path); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
