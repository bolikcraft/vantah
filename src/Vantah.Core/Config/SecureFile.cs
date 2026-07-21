// Vantah — только Linux, поэтому Unix-права доступны всегда (CA1416 предупреждает про Windows).
#pragma warning disable CA1416

namespace Vantah.Core.Config;

/// <summary>
/// Атомарная запись приватных файлов Vantah: временный файл в той же директории → rename поверх.
/// Временный файл СОЗДАЁТСЯ сразу с правами 600 (UnixCreateMode, минуя umask), а File.Move
/// переносит права как есть — поэтому ни временный, ни итоговый файл не бывает world-readable
/// даже мгновение. Каталог создаём с 700 — история подключений и активная сессия не должны
/// читаться другими локальными пользователями.
/// </summary>
public static class SecureFile
{
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode PrivateDir =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <param name="secureDirectory">
    /// false — каталог назначения чужой (экспорт в выбранную пользователем папку), его права не трогаем.
    /// </param>
    public static void WriteAllText(string path, string content, bool secureDirectory = true) =>
        Write(path, writer => writer.Write(content), secureDirectory);

    /// <inheritdoc cref="WriteAllText(string, string, bool)"/>
    public static void WriteAllLines(string path, IEnumerable<string> lines, bool secureDirectory = true) =>
        Write(path, writer => { foreach (var line in lines) writer.WriteLine(line); }, secureDirectory);

    private static void Write(string path, Action<StreamWriter> writeTo, bool secureDirectory)
    {
        // Пустой каталог = запись в текущую рабочую директорию, создавать нечего.
        var dir = Path.GetDirectoryName(path) ?? "";
        if (dir.Length > 0)
        {
            if (secureDirectory) EnsureDirectory(dir);
            else Directory.CreateDirectory(dir);
        }

        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            // UnixCreateMode задаёт права в момент создания — umask их не расширяет,
            // и файл ни на мгновение не бывает доступен другим пользователям.
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                UnixCreateMode = PrivateFile,
            };
            using (var writer = new StreamWriter(new FileStream(tmp, options)))
                writeTo(writer);

            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch { /* уборка best-effort */ }
            throw;
        }
    }

    /// <summary>Создаёт каталог с правами 700; уже существующему — тоже поджимает права.</summary>
    private static void EnsureDirectory(string dir)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir, PrivateDir);
            return;
        }

        // CreateDirectory задаёт режим только при создании — каталог от прежних версий мог остаться 755.
        try
        {
            var mode = File.GetUnixFileMode(dir);
            // Sticky-каталог (/tmp и подобные) общий по смыслу — его права не наши, не трогаем.
            if (mode != PrivateDir && !mode.HasFlag(UnixFileMode.StickyBit))
                File.SetUnixFileMode(dir, PrivateDir);
        }
        catch { /* чужой каталог/нет прав — не критично */ }
    }
}
