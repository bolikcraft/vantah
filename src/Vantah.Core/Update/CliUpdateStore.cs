using System.Text.Json;
using Vantah.Core.Config;

namespace Vantah.Core.Update;

/// <summary>Персист состояния проверки обновлений CLI — ~/.config/vantah/cliupdate.json.</summary>
public sealed class CliUpdateStore
{
    // Пишут сюда с UI-потока (крестик) и из фона (проверка обновлений) — замок обязателен,
    // см. AppUpdateStore.
    private readonly object _gate = new();
    private readonly string _path;

    public CliUpdateStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "cliupdate.json");
    }

    public CliUpdateState Load()
    {
        lock (_gate) return Read();
    }

    /// <summary>Изменить состояние: <paramref name="change"/> получает текущее. Возвращает записанное.</summary>
    public CliUpdateState Update(Func<CliUpdateState, CliUpdateState> change)
    {
        lock (_gate)
        {
            var next = change(Read());
            Write(next);
            return next;
        }
    }

    private CliUpdateState Read()
    {
        try
        {
            if (!File.Exists(_path)) return new CliUpdateState();
            return JsonSerializer.Deserialize<CliUpdateState>(File.ReadAllText(_path))
                   ?? new CliUpdateState();
        }
        catch { return new CliUpdateState(); }
    }

    private void Write(CliUpdateState state)
    {
        try
        {
            SecureFile.WriteAllText(_path, JsonSerializer.Serialize(state));
        }
        catch { /* не смогли сохранить — плашка необязательна */ }
    }
}
