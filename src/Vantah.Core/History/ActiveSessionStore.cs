using System.Text.Json;
using Vantah.Core.Config;

namespace Vantah.Core.History;

/// <summary>
/// Чистое IO: активная сессия как одиночный JSON-объект под XDG data dir.
/// Отдельно от <see cref="ConnectionHistoryStore"/> (там только завершённые сессии),
/// чтобы незавершённая сессия пережила перезапуск приложения: закрытие Vantah
/// не рвёт VPN-соединение, значит и сессию завершать нельзя.
/// </summary>
public sealed class ActiveSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _path;

    public ActiveSessionStore(string? path = null) =>
        _path = path ?? Path.Combine(VantahPaths.DataDir, "connection-active");

    public ActiveSessionState? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<ActiveSessionState>(File.ReadAllText(_path), JsonOptions);
        }
        catch { return null; }
    }

    public void Save(ActiveSessionState state)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        // Атомарная запись: сначала во временный файл в той же директории, затем move поверх.
        var tmp = Path.Combine(dir, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(tmp, _path, overwrite: true);
    }

    /// <summary>Активной сессии больше нет (её завершили и перенесли в историю).</summary>
    public void Clear()
    {
        try { File.Delete(_path); }
        catch { /* нет файла или нет прав — не критично */ }
    }
}
