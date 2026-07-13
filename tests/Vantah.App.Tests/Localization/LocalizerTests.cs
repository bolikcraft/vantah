using System.ComponentModel;
using System.Reflection;
using Vantah.App.Localization;

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
    public void Default_language_is_russian()
    {
        var loc = new Localizer();

        Assert.Equal("ru", loc.Language);
        Assert.Equal("Подключено", loc[LocKeys.Status_Connected]);
        Assert.Equal("Локации", loc[LocKeys.Tab_Locations]);
        Assert.Equal("Отмена", loc["Common_Cancel"]);
    }

    [Fact]
    public void SetLanguage_en_switches_values_to_english()
    {
        var loc = new Localizer();

        loc.SetLanguage("en");

        Assert.Equal("en", loc.Language);
        Assert.Equal("Connected", loc[LocKeys.Status_Connected]);
        Assert.Equal("Locations", loc[LocKeys.Tab_Locations]);
        Assert.Equal("Cancel", loc[LocKeys.Common_Cancel]);
    }

    [Fact]
    public void SetLanguage_back_to_ru_restores_russian()
    {
        var loc = new Localizer();

        loc.SetLanguage("en");
        loc.SetLanguage("ru");

        Assert.Equal("ru", loc.Language);
        Assert.Equal("Подключено", loc[LocKeys.Status_Connected]);
    }

    [Fact]
    public void Format_substitutes_arguments_in_russian()
    {
        var loc = new Localizer();

        Assert.Equal("Домены (7)", loc.Format(LocKeys.Tray_DomainsFormat, 7));
        Assert.Equal("Завершить процесс PID 1234?", loc.Format(LocKeys.Processes_KillConfirm, 1234));
    }

    [Fact]
    public void Format_substitutes_arguments_in_english()
    {
        var loc = new Localizer();

        loc.SetLanguage("en");

        Assert.Equal("Domains (7)", loc.Format(LocKeys.Tray_DomainsFormat, 7));
        Assert.Equal("Terminate process PID 1234?", loc.Format(LocKeys.Processes_KillConfirm, 1234));
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

        loc.SetLanguage("en");

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

        loc.SetLanguage("en");

        Assert.Equal("en", changed);
    }

    /// <summary>Забытый перевод: ключ есть в LocKeys, но значения в .resx нет — индексатор вернёт сам ключ.</summary>
    [Theory]
    [MemberData(nameof(Keys))]
    public void Every_LocKeys_key_is_translated_in_both_languages(string key)
    {
        var loc = new Localizer();

        Assert.NotEqual(key, loc[key]);
        Assert.False(string.IsNullOrWhiteSpace(loc[key]));

        loc.SetLanguage("en");

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

    [Fact]
    public void Ru_and_en_resx_declare_exactly_the_same_keys()
    {
        var ru = ResxKeys("Strings.resx");
        var en = ResxKeys("Strings.en.resx");

        Assert.Empty(ru.Except(en));   // нет перевода в en
        Assert.Empty(en.Except(ru));   // лишний ключ в en
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
        var path = Path.Combine(RepoRoot(), "src", "Vantah.App", "Localization", fileName);
        var doc = System.Xml.Linq.XDocument.Load(path);
        return doc.Root!
            .Elements("data")
            .Select(d => d.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vantah.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
