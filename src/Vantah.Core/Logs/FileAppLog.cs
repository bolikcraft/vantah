// Vantah — только Linux, поэтому Unix-права доступны всегда (CA1416 предупреждает про Windows).
#pragma warning disable CA1416

using System.Globalization;
using System.Text;
using Vantah.Core.Config;

namespace Vantah.Core.Logs;

/// <summary>
/// Файловый лог приложения — ~/.local/share/vantah/app.log. При переполнении файл уезжает
/// в app.log.1 (архив ровно один, старый перезаписывается).
/// </summary>
public sealed class FileAppLog : IAppLog
{
    private const long DefaultMaxBytes = 1024 * 1024;

    // Без BOM: иначе первая строка нового файла получает три лишних байта.
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode PrivateDir =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _archivePath;
    private readonly long _maxBytes;

    // Размер файла ведём сами: писатель тут единственный, а FileInfo на каждой строке — лишний syscall.
    // -1 = ещё не читали с диска.
    private long _size = -1;

    private volatile bool _enabled;

    public FileAppLog(string? path = null, long maxBytes = DefaultMaxBytes)
    {
        _path = path ?? Path.Combine(VantahPaths.DataDir, "app.log");
        _archivePath = _path + ".1";
        _maxBytes = maxBytes > 0 ? maxBytes : DefaultMaxBytes;
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public void Write(string message)
    {
        if (!_enabled) return;

        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var line = $"{stamp} {message}\n";

        lock (_gate)
        {
            try { Append(line); }
            catch { /* лог не имеет права уронить приложение */ }
        }
    }

    private void Append(string line)
    {
        var dir = Path.GetDirectoryName(_path) ?? "";
        if (dir.Length > 0 && !Directory.Exists(dir)) Directory.CreateDirectory(dir, PrivateDir);

        if (_size < 0) _size = File.Exists(_path) ? new FileInfo(_path).Length : 0;

        var bytes = Encoding.UTF8.GetByteCount(line);
        // Пустой файл не ротируем, иначе одна слишком длинная строка крутила бы архив вхолостую.
        if (_size > 0 && _size + bytes > _maxBytes)
        {
            File.Move(_path, _archivePath, overwrite: true);
            _size = 0;
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.Append,
            Access = FileAccess.Write,
            UnixCreateMode = PrivateFile,
        };
        using (var writer = new StreamWriter(new FileStream(_path, options), Utf8NoBom))
            writer.Write(line);

        _size += bytes;
    }
}
