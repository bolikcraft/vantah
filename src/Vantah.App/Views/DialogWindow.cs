using Avalonia.Controls;

namespace Vantah.App.Views;

/// <summary>
/// Служебное окно: заголовок и слот контента. Закрытие крестиком не разрушает окно, а прячет —
/// контент (вью со своей вьюмоделью) остаётся подключённым и не теряет состояние.
/// </summary>
public sealed class DialogWindow : Window
{
    public DialogWindow(string title, Control content)
    {
        Title = title;
        Content = content;
        Width = 560;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }
}
