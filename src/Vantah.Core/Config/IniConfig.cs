namespace Vantah.Core.Config;

/// <summary>
/// Минимальный INI: одна безымянная секция, строки вида «ключ = значение».
/// Комментарии — с «#» или «;», строки-секции «[...]» пропускаются, ключи регистронезависимы.
/// Пустое значение равносильно отсутствию ключа (<see cref="Get" /> вернёт null).
/// </summary>
public sealed class IniConfig
{
    private readonly IReadOnlyDictionary<string, string> _values;

    private IniConfig(IReadOnlyDictionary<string, string> values) => _values = values;

    /// <summary>Пустой конфиг: любой <see cref="Get" /> вернёт null.</summary>
    public static IniConfig Empty { get; } = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Значение ключа или null, если ключа нет либо значение пустое.</summary>
    public string? Get(string key) => _values.GetValueOrDefault(key);

    public static IniConfig Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // BOM: File.ReadAllText его съедает сам, но Parse зовут и с сырой строкой —
        // иначе первый ключ файла молча «терялся» бы вместе с невидимым символом.
        foreach (var raw in text.TrimStart('﻿').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';' or '[') continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0) continue;

            values[key] = value;
        }

        return new IniConfig(values);
    }

    /// <summary>
    /// Читает конфиг из файла. Нет файла или любая ошибка чтения (права, битая кодировка) —
    /// это не повод падать при старте: возвращается пустой конфиг, дальше сработают дефолты.
    /// </summary>
    public static IniConfig Load(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;
        }
        catch { return Empty; }
    }
}
