using System.Text.Json;
using Vantah.Core.Config;

namespace Vantah.Core.Update;

/// <summary>Персист состояния проверки обновлений — ~/.config/vantah/appupdate.json.</summary>
public sealed class AppUpdateStore
{
    // В этот файл пишут двое: UI-поток (галка в настройках) и фоновая проверка обновлений.
    // Совпадение записей дало бы IOException прямо из сеттера привязанного свойства, поэтому
    // доступ сериализуем, а запись глушим — настройка не стоит падения UI.
    private readonly object _gate = new();
    private readonly string _path;

    public AppUpdateStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "appupdate.json");
    }

    public AppUpdateState Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_path)) return new AppUpdateState();
                return JsonSerializer.Deserialize<AppUpdateState>(File.ReadAllText(_path))
                       ?? new AppUpdateState();
            }
            catch { return new AppUpdateState(); }
        }
    }

    public void Save(AppUpdateState state)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(state));
            }
            catch { /* не смогли сохранить — переживём: проверка обновлений необязательна */ }
        }
    }
}
