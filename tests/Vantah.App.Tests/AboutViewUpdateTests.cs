using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantah.App.Localization;
using Vantah.App.Tests.Fakes;
using Vantah.App.ViewModels;
using Vantah.App.Views;
using Vantah.Core.Localization;
using Vantah.Core.Update;

/// <summary>Ссылка «Доступна новая версия» на экране «О программе».</summary>
public class AboutViewUpdateTests
{
    private static readonly AppUpdateInfo Release =
        new("v0.7.0", "https://github.com/bolikcraft/vantah/releases/tag/v0.7.0");

    private static (AboutViewModel Vm, Window Window) Show()
    {
        var vm = new AboutViewModel(new FakeVpnService());
        var window = new Window { Content = new AboutView { DataContext = vm }, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (vm, window);
    }

    /// <summary>Видимые ссылки экрана, кроме постоянной ссылки на репозиторий.</summary>
    private static List<HyperlinkButton> UpdateLinks(Window window) =>
        window.GetVisualDescendants().OfType<HyperlinkButton>()
            .Where(b => b.IsEffectivelyVisible && b.NavigateUri != new Uri(AboutViewModel.RepositoryUrl))
            .ToList();

    // Подпись ссылки — TextBlock с переносом: длинные переводы в узком окне иначе обрезаются.
    private static string? LinkText(HyperlinkButton link) => Assert.IsType<TextBlock>(link.Content).Text;

    [AvaloniaFact]
    public void No_update_link_while_no_newer_release_is_known()
    {
        var (_, window) = Show();

        Assert.Empty(UpdateLinks(window));
    }

    [AvaloniaFact]
    public void Update_link_shows_the_version_and_leads_to_the_release_page()
    {
        var (vm, window) = Show();

        vm.ShowUpdate(Release);
        Dispatcher.UIThread.RunJobs();

        var link = Assert.Single(UpdateLinks(window));
        Assert.Equal("A new version of Vantah is available — 0.7.0", LinkText(link));
        Assert.Equal(new Uri(Release.ReleaseUrl), link.NavigateUri);
    }

    [AvaloniaFact]
    public void Update_link_disappears_when_the_release_is_gone()
    {
        var (vm, window) = Show();
        vm.ShowUpdate(Release);
        Dispatcher.UIThread.RunJobs();

        vm.ShowUpdate(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(UpdateLinks(window));
    }

    [AvaloniaFact]
    public void Update_link_falls_back_to_the_latest_release_page_for_a_broken_url()
    {
        // Релиз читается из appupdate.json, а файл мог испортиться: ссылка не должна ронять экран.
        var (vm, window) = Show();

        vm.ShowUpdate(new AppUpdateInfo("v0.7.0", "not a url"));
        Dispatcher.UIThread.RunJobs();

        var link = Assert.Single(UpdateLinks(window));
        Assert.Equal(new Uri("https://github.com/bolikcraft/vantah/releases/latest"), link.NavigateUri);
    }

    [AvaloniaFact]
    public void Update_link_text_follows_a_language_switch()
    {
        var (vm, window) = Show();
        vm.ShowUpdate(Release);
        Dispatcher.UIThread.RunJobs();

        try
        {
            Localizer.Instance.SetLanguage("ru");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Доступна новая версия Vantah — 0.7.0", LinkText(Assert.Single(UpdateLinks(window))));
        }
        finally
        {
            // Localizer.Instance — синглтон на весь тест-хост: не вернём язык — протечёт в соседей.
            Localizer.Instance.SetLanguage(CultureSelector.Default);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
