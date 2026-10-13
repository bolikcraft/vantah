using System.Text.Json;
using Vantah.Core.Config;

namespace Vantah.Core.Update;

/// <summary>Персист состояния проверки обновлений — ~/.config/vantah/appupdate.json.</summary>
public sealed class AppUpdateStore
{
    // В этот файл пишут двое: UI-поток (тумблер, крестик) и фоновая проверка обновлений.
    // Меняют его только через Update: чтение и запись под одним замком, иначе один писатель
    // затирает изменение другого. Сбой записи глушим — настройка не стоит падения UI.
    private readonly object _gate = new();
    private readonly string _path;

    public AppUpdateStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "appupdate.json");
    }

    public AppUpdateState Load()
    {
        lock (_gate) return Read();
    }

    /// <summary>Изменить состояние: <paramref name="change"/> получает текущее. Возвращает записанное.</summary>
    public AppUpdateState Update(Func<AppUpdateState, AppUpdateState> change)
    {
        lock (_gate)
        {
            var next = change(Read());
            Write(next);
            return next;
        }
    }

    private AppUpdateState Read()
    {
        try
        {
            if (!File.Exists(_path)) return new AppUpdateState();
            return JsonSerializer.Deserialize<AppUpdateState>(File.ReadAllText(_path))
                   ?? new AppUpdateState();
        }
        catch { return new AppUpdateState(); }
    }

    private void Write(AppUpdateState state)
    {
        try
        {
            SecureFile.WriteAllText(_path, JsonSerializer.Serialize(state));
        }
        catch { /* не смогли сохранить — переживём: проверка обновлений необязательна */ }
    }
}
