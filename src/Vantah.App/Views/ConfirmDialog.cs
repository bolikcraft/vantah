using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Vantah.App.Views;

/// <summary>
/// Простое модальное подтверждение через Window, без зависимости от темы FluentAvalonia.
/// Общее для всех необратимых действий (очистка исключений, выход из аккаунта).
/// </summary>
public static class ConfirmDialog
{
    /// <summary>
    /// Показывает модальное «да/нет» поверх окна, которому принадлежит <paramref name="anchor"/>.
    /// Возвращает false, если владельца нет (например, контрол ещё не в дереве) — молчаливый
    /// отказ безопаснее, чем выполнить необратимое действие без подтверждения.
    /// </summary>
    public static async Task<bool> ShowAsync(Visual anchor, string title, string message, string confirmText,
                                             string cancelText)
    {
        if (TopLevel.GetTopLevel(anchor) is not Window owner) return false;

        var result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var ok = new Button { Content = confirmText, IsDefault = true };
        var cancel = new Button { Content = cancelText, IsCancel = true };
        ok.Click += (_, _) => { result = true; dialog.Close(); };
        cancel.Click += (_, _) => { result = false; dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, ok },
                },
            },
        };

        await dialog.ShowDialog(owner);
        return result;
    }
}
