using System.ComponentModel;
using System.Reflection;
using Vantah.App.Localization;
using Vantah.App.ViewModels;
using Vantah.Core.Localization;

namespace Vantah.App.Tests.Localization;

/// <summary>
/// Тесты берут собственный экземпляр <see cref="Localizer"/> (internal-конструктор),
/// а не <see cref="Localizer.Instance"/>: синглтон общий на весь тест-хост, и смена
/// языка в одном тесте протекла бы в остальные.
/// </summary>
public class LocalizerTests
{
    /// <summary>Все ключи, объявленные в <see cref="LocKeys"/>, — через рефлексию по const-полям.</summary>
    private static IReadOnlyList<string> AllKeys() =>
        typeof(LocKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    public static TheoryData<string> Keys()
    {
        var data = new TheoryData<string>();
        foreach (var key in AllKeys()) data.Add(key);
        return data;
    }

    [Fact]
    public void Default_language_is_english()
    {
        var loc = new Localizer();

        Assert.Equal("en", loc.Language);
        Assert.Equal("Connected", loc[LocKeys.Status_Connected]);
        Assert.Equal("Locations", loc[LocKeys.Tab_Locations]);
        Assert.Equal("Cancel", loc["Common_Cancel"]);
    }

    [Fact]
    public void SetLanguage_ru_switches_values_to_russian()
    {
        var loc = new Localizer();

        loc.SetLanguage("ru");

        Assert.Equal("ru", loc.Language);
        Assert.Equal("Подключено", loc[LocKeys.Status_Connected]);
        Assert.Equal("Локации", loc[LocKeys.Tab_Locations]);
        Assert.Equal("Отмена", loc[LocKeys.Common_Cancel]);
    }

    [Fact]
    public void SetLanguage_back_to_en_restores_english()
    {
        var loc = new Localizer();

        loc.SetLanguage("ru");
        loc.SetLanguage("en");

        Assert.Equal("en", loc.Language);
        Assert.Equal("Connected", loc[LocKeys.Status_Connected]);
    }

    [Fact]
    public void Format_substitutes_arguments_in_english()
    {
        var loc = new Localizer();

        Assert.Equal("Domains (7)", loc.Format(LocKeys.Tray_DomainsFormat, 7));
        Assert.Equal("Terminate process PID 1234?", loc.Format(LocKeys.Processes_KillConfirm, 1234));
    }

    [Fact]
    public void Format_substitutes_arguments_in_russian()
    {
        var loc = new Localizer();

        loc.SetLanguage("ru");

        Assert.Equal("Домены (7)", loc.Format(LocKeys.Tray_DomainsFormat, 7));
        Assert.Equal("Завершить процесс PID 1234?", loc.Format(LocKeys.Processes_KillConfirm, 1234));
    }

    [Fact]
    public void Unknown_key_returns_the_key_itself()
    {
        var loc = new Localizer();

        Assert.Equal("Nope_Missing_Key", loc["Nope_Missing_Key"]);
        Assert.Equal("Nope_Missing_Key", loc.Format("Nope_Missing_Key", 1));
    }

    [Fact]
    public void SetLanguage_raises_PropertyChanged_for_indexer()
    {
        var loc = new Localizer();
        var names = new List<string?>();
        ((INotifyPropertyChanged)loc).PropertyChanged += (_, e) => names.Add(e.PropertyName);

        loc.SetLanguage("ru");

        // "Item[]" перечитывает XAML-привязки-индексаторы, "" (string.Empty) — все остальные.
        Assert.Contains("Item[]", names);
        Assert.Contains(string.Empty, names);
    }

    [Fact]
    public void SetLanguage_raises_LanguageChanged()
    {
        var loc = new Localizer();
        string? changed = null;
        loc.LanguageChanged += (_, _) => changed = loc.Language;

        loc.SetLanguage("ru");

        Assert.Equal("ru", changed);
    }

    /// <summary>Забытый перевод: ключ есть в LocKeys, но значения в .resx нет — индексатор вернёт сам ключ.</summary>
    [Theory]
    [MemberData(nameof(Keys))]
    public void Every_LocKeys_key_is_translated_in_both_languages(string key)
    {
        var loc = new Localizer();

        Assert.NotEqual(key, loc[key]);
        Assert.False(string.IsNullOrWhiteSpace(loc[key]));

        loc.SetLanguage("ru");

        Assert.NotEqual(key, loc[key]);
        Assert.False(string.IsNullOrWhiteSpace(loc[key]));
    }

    [Fact]
    public void LocKeys_is_not_empty()
    {
        // Страховка: если рефлексия сломается и вернёт пустой список,
        // теория выше станет зелёной, ничего не проверив.
        Assert.True(AllKeys().Count >= 78);
    }

    /// <summary>
    /// Языки интерфейса, кроме английского: у него значения лежат в нейтральном Strings.resx,
    /// отдельного Strings.en.resx и сателлитной сборки en нет. Русский — такой же сателлит,
    /// как остальные, и проверяется наравне с ними.
    /// </summary>
    public static TheoryData<string> TranslatedLanguages()
    {
        var data = new TheoryData<string>();
        foreach (var code in CultureSelector.Supported.Where(c => c != CultureSelector.Default))
            data.Add(code);
        return data;
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void Every_language_resx_declares_the_same_keys_as_neutral(string code)
    {
        var neutral = ResxKeys("Strings.resx");
        var translated = ResxKeys($"Strings.{code}.resx");

        Assert.Empty(neutral.Except(translated));   // нет перевода
        Assert.Empty(translated.Except(neutral));   // лишний ключ
    }

    /// <summary>
    /// Потерянный или лишний плейсхолдер в переводе — это не кривая строка, а падение
    /// <c>string.Format</c> в рантайме (FormatException) либо молча пропавшее значение.
    /// </summary>
    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void Every_language_keeps_the_placeholders_of_the_neutral_string(string code)
    {
        var neutral = ResxValues("Strings.resx");
        var translated = ResxValues($"Strings.{code}.resx");

        foreach (var (key, value) in neutral)
        {
            Assert.True(translated.ContainsKey(key), $"{code}: нет ключа {key}");
            Assert.Equal(Placeholders(value), Placeholders(translated[key]));
        }
    }

    /// <summary>
    /// Главная страховка этого файла. Забытый (или не попавший в сборку) сателлитный ресурс
    /// <c>ResourceManager</c> не считает ошибкой: он молча отдаёт нейтральные — английские —
    /// строки, и все проверки «строка непустая и не равна ключу» остаются зелёными на
    /// полностью непереведённом языке. Поэтому спрашиваем именно сателлит:
    /// <c>tryParents: false</c> — никакого отката на нейтральный ресурс.
    /// </summary>
    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void Every_language_ships_its_own_satellite_assembly(string code)
    {
        var resources = new System.Resources.ResourceManager(
            "Vantah.App.Localization.Strings", typeof(Localizer).Assembly);

        var set = resources.GetResourceSet(new System.Globalization.CultureInfo(code),
            createIfNotExists: true, tryParents: false);

        Assert.True(set is not null, $"{code}: сателлитная сборка не собрана или не найдена");
        foreach (var key in AllKeys())
            Assert.False(string.IsNullOrWhiteSpace(set!.GetString(key)), $"{code}: нет строки {key}");
    }

    /// <summary>Язык без строки в выпадающем списке настроек выбрать нельзя — и наоборот.</summary>
    [Fact]
    public void Language_dropdown_matches_supported_cultures()
    {
        var dropdown = ConfigViewModel.AllLanguages.Select(l => l.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(CultureSelector.Supported.ToHashSet(StringComparer.Ordinal), dropdown);
        Assert.All(ConfigViewModel.AllLanguages, l => Assert.False(string.IsNullOrWhiteSpace(l.Display)));
    }

    /// <summary>Каждый язык действительно отдаёт переведённые строки, а не откатывается на нейтральные.</summary>
    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void Every_language_translates_every_key(string code)
    {
        var loc = new Localizer();
        loc.SetLanguage(code);

        Assert.Equal(code, loc.Language);
        foreach (var key in AllKeys())
        {
            Assert.NotEqual(key, loc[key]);                          // ключ вместо строки = забытый перевод
            Assert.False(string.IsNullOrWhiteSpace(loc[key]), key);
        }
    }

    [Fact]
    public void Resx_keys_and_LocKeys_match_exactly()
    {
        var resx = ResxKeys("Strings.resx");
        var lockeys = AllKeys().ToHashSet(StringComparer.Ordinal);

        Assert.Empty(resx.Except(lockeys));   // строка в .resx без константы в LocKeys
        Assert.Empty(lockeys.Except(resx));   // константа в LocKeys без строки в .resx
    }

    /// <summary>Имена data-элементов из .resx, взятого прямо из исходников (не из скомпилированного ресурса).</summary>
    private static HashSet<string> ResxKeys(string fileName)
    {
        var doc = System.Xml.Linq.XDocument.Load(ResxPath(fileName));
        return doc.Root!
            .Elements("data")
            .Select(d => d.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Пары «ключ → значение» из .resx исходников.</summary>
    private static string ResxPath(string fileName) =>
        Path.Combine(RepoRoot(), "src", "Vantah.App", "Localization", fileName);

    private static Dictionary<string, string> ResxValues(string fileName)
    {
        var doc = System.Xml.Linq.XDocument.Load(ResxPath(fileName));
        return doc.Root!
            .Elements("data")
            .ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")!.Value, StringComparer.Ordinal);
    }

    /// <summary>Плейсхолдеры вида {0}: сравниваем как множество — порядок в переводе может отличаться.</summary>
    private static HashSet<string> Placeholders(string value) =>
        System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}")
            .Select(m => m.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vantah.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
