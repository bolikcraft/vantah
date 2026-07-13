using Vantah.Core.Config;

namespace Vantah.Core.Localization;

/// <summary>Персист выбранного пользователем языка — ~/.config/vantah/language.</summary>
public sealed class LanguageStore
{
    private readonly string _path;

    public LanguageStore(string? path = null)
    {
        _path = path ?? Path.Combine(VantahPaths.ConfigDir, "language");
    }

    /// <summary>Сохранённый код языка или null, если выбор не сделан либо файл нечитаем.</summary>
    public string? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var code = File.ReadAllText(_path).Trim();
            return string.IsNullOrWhiteSpace(code) ? null : code;
        }
        catch { return null; }
    }

    public void Save(string code)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, code.Trim());
    }
}
