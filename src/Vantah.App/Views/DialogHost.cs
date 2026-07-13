using System;
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

    /// <summary>
    /// Открывает окно по ключу, создавая его при первом обращении. Контент задаётся фабрикой,
    /// а не готовым контролом: вью с её вьюмоделью нужна ровно одна на всю сессию, и лишний
    /// экземпляр не должен даже создаваться — иначе повторный клик по пункту меню молча
    /// выбрасывал бы свежесозданную вью.
    /// </summary>
    public DialogWindow Open(string key, string title, Func<Control> createContent, Window owner)
    {
        if (!_windows.TryGetValue(key, out var window))
        {
            window = new DialogWindow(title, createContent());
            _windows[key] = window;
        }

        // Заголовок перечитываем: язык интерфейса мог смениться, пока окно было спрятано.
        window.Title = title;

        // Владельца передаём КАЖДЫЙ раз, а не только при создании: Avalonia проставляет Owner
        // внутри Show(owner), и голый Show() на спрятанном окне обнуляет его. Без владельца
        // CenterOwner молча откатывается на CenterScreen, а оконный менеджер Linux перестаёт
        // держать служебное окно поверх главного (transient-for). Повторный Show(owner) окно
        // владельцу второй раз не приписывает — дубля в списке детей не будет.
        window.Show(owner);
        window.Activate();
        return window;
    }
}
