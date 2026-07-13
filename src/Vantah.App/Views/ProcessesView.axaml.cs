using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace Vantah.App.Views;

public partial class ProcessesView : UserControl
{
    public ProcessesView() => InitializeComponent();

    // Кнопка подтверждения внутри Flyout сама поповер не закрывает — закрываем его вручную.
    // ВАЖНО: закрывать строго следующим тиком. Button.OnClick сначала поднимает Click и только
    // потом выполняет Command; закрытие Popup прямо здесь отцепляет кнопку от дерева, её
    // DataContext обнуляется, биндинг проталкивает null в Command — и команда не выполняется.
    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Visual v && v.FindLogicalAncestorOfType<Popup>() is { } popup)
            Dispatcher.UIThread.Post(popup.Close);
    }
}
