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
    [AvaloniaFact]
    public void Open_shows_a_window_with_the_given_content()
    {
        var host = new DialogHost();
        var content = new TextBlock { Text = "содержимое" };

        var window = host.Open("settings", "Настройки", content);

        Assert.True(window.IsVisible);
        Assert.Equal("Настройки", window.Title);
        Assert.Same(content, window.Content);
    }

    [AvaloniaFact]
    public void Opening_the_same_key_twice_reuses_one_window()
    {
        var host = new DialogHost();

        var first = host.Open("settings", "Настройки", new TextBlock());
        var second = host.Open("settings", "Настройки", new TextBlock());

        Assert.Same(first, second);
    }

    [AvaloniaFact]
    public void Different_keys_get_different_windows()
    {
        var host = new DialogHost();

        var settings = host.Open("settings", "Настройки", new TextBlock());
        var about = host.Open("about", "О программе", new TextBlock());

        Assert.NotSame(settings, about);
    }

    // Контент переживает закрытие: окно прячется, а не разрушается.
    [AvaloniaFact]
    public void Closing_hides_the_window_and_reopening_shows_the_same_content()
    {
        var host = new DialogHost();
        var content = new TextBlock { Text = "содержимое" };
        var window = host.Open("settings", "Настройки", content);

        window.Close();

        Assert.False(window.IsVisible);

        var reopened = host.Open("settings", "Настройки", content);

        Assert.Same(window, reopened);
        Assert.True(reopened.IsVisible);
        Assert.Same(content, reopened.Content);
    }
}
