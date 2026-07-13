using System.Collections.Generic;
using Avalonia.Controls;

namespace Vantah.App.Views;

/// <summary>
/// Реестр служебных окон: одно окно на ключ. Повторный клик по пункту меню поднимает
/// уже открытое окно, а не плодит второе.
/// </summary>
public sealed class DialogHost
{
    private readonly Dictionary<string, DialogWindow> _windows = new();

    public DialogWindow Open(string key, string title, Control content)
    {
        if (!_windows.TryGetValue(key, out var window))
        {
            window = new DialogWindow(title, content);
            _windows[key] = window;
        }

        // Заголовок перечитываем: язык интерфейса мог смениться, пока окно было спрятано.
        window.Title = title;
        window.Show();
        window.Activate();
        return window;
    }
}
