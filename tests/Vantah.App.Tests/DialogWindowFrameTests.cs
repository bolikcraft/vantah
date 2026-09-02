using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Vantah.App.Views;
using Xunit;

/// <summary>
/// Служебное окно открывается поверх главного, фон у них один и тот же, и без рамки край окна
/// теряется — окна «слипаются». Рамку рисует стиль, а стиль поверх шаблона молча не срабатывает,
/// если шаблон задал значение локально, поэтому проверяем не свойства, а сам кадр.
/// </summary>
public class DialogWindowFrameTests
{
    private static uint PixelAt(WriteableBitmap frame, int x, int y)
    {
        using var buffer = frame.Lock();
        return (uint)Marshal.ReadInt32(buffer.Address + y * buffer.RowBytes + x * 4);
    }

    private static DialogWindow Shown(ThemeVariant theme)
    {
        var window = new DialogWindow(() => "Настройки", new TextBlock { Text = "содержимое" }, 200, 200)
        {
            RequestedThemeVariant = theme,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_service_window_draws_a_frame_along_its_edge(string themeName)
    {
        var window = Shown(themeName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);

        using var frame = window.CaptureRenderedFrame()!;
        var middle = frame.PixelSize.Height / 2;
        var edge = PixelAt(frame, 0, middle);
        var inside = PixelAt(frame, 20, middle);

        Assert.NotEqual(inside, edge);
    }
}
