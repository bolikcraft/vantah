using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Vantah.Core.Localization;

namespace Vantah.App.Localization;

/// <summary>
/// Провайдер локализованных строк поверх <c>Strings.resx</c> (нейтральная культура — русская).
/// </summary>
/// <remarks>
/// XAML тянет строки через индексатор:
/// <code>{Binding [Tab_Status], Source={x:Static loc:Localizer.Instance}}</code>
/// Смена языка на лету работает потому, что <see cref="SetLanguage"/> поднимает
/// <c>PropertyChanged</c> с именем «Item[]» — привязки-индексаторы перечитывают значение.
/// Код (ViewModels, трей), который держит строки в полях, подписывается на
/// <see cref="LanguageChanged"/> и обновляет их сам.
/// </remarks>
public sealed class Localizer : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources =
        new("Vantah.App.Localization.Strings", typeof(Localizer).Assembly);

    private CultureInfo _culture = new(CultureSelector.Default);

    /// <summary>Экземпляр для приложения. Тесты создают свой через internal-конструктор.</summary>
    public static Localizer Instance { get; } = new();

    internal Localizer()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Язык сменился. Для кода, который кэширует строки в полях (ViewModels, меню трея).</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>Двухбуквенный код текущего языка интерфейса.</summary>
    /// <summary>
    /// Код текущего языка интерфейса — имя культуры, а не две буквы: у португальского и
    /// китайского вариант значим («pt-BR», «zh-Hans»), и по этому же коду язык ищется
    /// в списке <see cref="CultureSelector.Supported"/> и в выпадающем списке настроек.
    /// </summary>
    public string Language => _culture.Name;

    /// <summary>Строка по ключу; неизвестный ключ возвращается как есть — UI не падает из-за опечатки.</summary>
    public string this[string key] => Resources.GetString(key, _culture) ?? key;

    /// <summary>Строка по ключу с подстановкой аргументов по текущей культуре.</summary>
    public string Format(string key, params object[] args) =>
        string.Format(_culture, this[key], args);

    /// <summary>
    /// Переключает язык интерфейса. Неподдерживаемый код откатывается к
    /// <see cref="CultureSelector.Default"/>.
    /// </summary>
    public void SetLanguage(string code)
    {
        var resolved = CultureSelector.Resolve(code, CultureInfo.InvariantCulture);
        _culture = new CultureInfo(resolved);

        // Культура нужна и потоку: string.Format/ToString в коде и StringFormat в XAML
        // берут её из CurrentUICulture, а новые потоки — из DefaultThreadCurrentUICulture.
        CultureInfo.CurrentUICulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;

        OnPropertyChanged(nameof(Language));
        OnPropertyChanged("Item[]");     // перечитать все привязки-индексаторы
        OnPropertyChanged(string.Empty); // перечитать всё остальное
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
