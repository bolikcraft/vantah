using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Vantah.App.Views;

public partial class ProcessesView : UserControl
{
    public ProcessesView() => InitializeComponent();

    // Кнопка подтверждения внутри Flyout сама поповер не закрывает — закрываем его вручную.
    // Событие Click мы не помечаем обработанным, поэтому привязанная Command всё равно выполнится.
    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Visual v)
            v.FindLogicalAncestorOfType<Popup>()?.Close();
    }
}
