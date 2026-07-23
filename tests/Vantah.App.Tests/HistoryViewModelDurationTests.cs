using System;
using Avalonia.Headless.XUnit;
using Vantah.App.ViewModels;
using Vantah.Core.History;
using Xunit;

// FormatEntry читает общий синглтон Localizer.Instance напрямую. Через [AvaloniaFact]
// тест попадает в ту же последовательную группу, что и остальные тесты синглтона
// (ErrorLocalizationTests, DialogHostTests): смена языка не пересекается с их проверками.
public class HistoryViewModelDurationTests
{
    private static ConnectionHistoryEntry Entry(DateTimeOffset start, DateTimeOffset? end) =>
        new("AMSTERDAM", "Netherlands", 12, start, end);

    [AvaloniaFact]
    public void Completed_session_line_shows_duration()
    {
        var prev = Vantah.App.Localization.Localizer.Instance.Language;
        Vantah.App.Localization.Localizer.Instance.SetLanguage("en");
        try
        {
            var start = new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero);
            var end = new DateTimeOffset(2026, 7, 23, 12, 15, 0, TimeSpan.Zero);
            var line = HistoryViewModel.FormatEntry(Entry(start, end));
            Assert.Contains("(2h 15m)", line);
        }
        finally { Vantah.App.Localization.Localizer.Instance.SetLanguage(prev); }
    }

    [AvaloniaFact]
    public void Active_session_line_has_no_duration()
    {
        var start = new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero);
        var line = HistoryViewModel.FormatEntry(Entry(start, null));
        Assert.DoesNotContain("(", line);
    }
}
