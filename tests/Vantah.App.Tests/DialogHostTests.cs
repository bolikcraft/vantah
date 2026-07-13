using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Vantah.App.Views;
using Xunit;

/// <summary>
/// Реестр служебных окон: одно окно на пункт меню, повторное открытие поднимает то же самое.
/// Закрытие окна его не разрушает — иначе контент (вью со своей вьюмоделью) пришлось бы
/// переподключать к новому родителю, а вьюмодель теряла бы состояние.
/// </summary>
public class DialogHostTests
{
    /// <summary>Окно-владелец: без него CenterOwner мёртв, а на Linux теряется transient-for.</summary>
    private static Window Owner()
    {
        var owner = new Window();
        owner.Show();
        return owner;
    }

    [AvaloniaFact]
    public void Open_shows_a_window_with_the_given_content()
    {
        var host = new DialogHost();
        var content = new TextBlock { Text = "содержимое" };

        var window = host.Open("settings", "Настройки", () => content, Owner());

        Assert.True(window.IsVisible);
        Assert.Equal("Настройки", window.Title);
        Assert.Same(content, window.Content);
    }

    [AvaloniaFact]
    public void Opening_the_same_key_twice_reuses_one_window()
    {
        var host = new DialogHost();
        var owner = Owner();

        var first = host.Open("settings", "Настройки", () => new TextBlock(), owner);
        var second = host.Open("settings", "Настройки", () => new TextBlock(), owner);

        Assert.Same(first, second);
    }

    [AvaloniaFact]
    public void Different_keys_get_different_windows()
    {
        var host = new DialogHost();
        var owner = Owner();

        var settings = host.Open("settings", "Настройки", () => new TextBlock(), owner);
        var about = host.Open("about", "О программе", () => new TextBlock(), owner);

        Assert.NotSame(settings, about);
    }

    /// <summary>
    /// Вью с её вьюмоделью нужна ровно одна. Вызывающий код создаёт вью на каждый клик по пункту
    /// меню, и если бы контент передавался готовым, лишний экземпляр молча выбрасывался бы —
    /// поэтому фабрика, и дёргается она только когда окно действительно создаётся.
    /// </summary>
    [AvaloniaFact]
    public void The_content_factory_is_not_called_again_when_the_window_is_reused()
    {
        var host = new DialogHost();
        var owner = Owner();
        var created = 0;

        Control Create()
        {
            created++;
            return new TextBlock { Text = "содержимое" };
        }

        var first = host.Open("settings", "Настройки", Create, owner);
        var second = host.Open("settings", "Настройки", Create, owner);

        Assert.Equal(1, created);
        Assert.Same(first.Content, second.Content);
    }

    // Без владельца CenterOwner молча откатывается на CenterScreen, а оконный менеджер Linux
    // не группирует служебное окно с главным.
    [AvaloniaFact]
    public void The_window_gets_the_owner_it_was_opened_from()
    {
        var host = new DialogHost();
        var owner = Owner();

        var window = host.Open("settings", "Настройки", () => new TextBlock(), owner);

        Assert.Same(owner, window.Owner);
    }

    /// <summary>
    /// Владелец переживает закрытие. Avalonia проставляет Owner внутри Show(owner), а голый
    /// Show() на спрятанном окне его ОБНУЛЯЕТ — поэтому владельца надо передавать при каждом
    /// открытии, иначе после первого же закрытия окно теряет связь с главным.
    /// </summary>
    [AvaloniaFact]
    public void The_owner_survives_closing_and_reopening()
    {
        var host = new DialogHost();
        var owner = Owner();
        var window = host.Open("settings", "Настройки", () => new TextBlock(), owner);

        window.Close();
        var reopened = host.Open("settings", "Настройки", () => new TextBlock(), owner);

        Assert.Same(owner, reopened.Owner);
    }

    // Контент переживает закрытие: окно прячется, а не разрушается.
    [AvaloniaFact]
    public void Closing_hides_the_window_and_reopening_shows_the_same_content()
    {
        var host = new DialogHost();
        var owner = Owner();
        var content = new TextBlock { Text = "содержимое" };
        var window = host.Open("settings", "Настройки", () => content, owner);

        window.Close();

        Assert.False(window.IsVisible);

        var reopened = host.Open("settings", "Настройки", () => content, owner);

        Assert.Same(window, reopened);
        Assert.True(reopened.IsVisible);
        Assert.Same(content, reopened.Content);
    }
}
