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
    /// выбрасывал бы свежесозданную вью. Заголовок — тоже фабрика: окно живёт до конца сессии
    /// и обязано пережить смену языка интерфейса.
    /// </summary>
    public DialogWindow Open(string key, Func<string> title, Func<Control> createContent, Window owner,
                             double width = 560, double height = 720)
    {
        if (!_windows.TryGetValue(key, out var window))
        {
            window = new DialogWindow(title, createContent(), width, height);
            _windows[key] = window;
        }

        // Владельца передаём КАЖДЫЙ раз, а не только при создании: Hide() (а закрытие окна у нас
        // как раз прячет) обнуляет Owner и отцепляет окно от списка детей владельца, а голый Show()
        // владельца не восстанавливает — окно всплыло бы уже ничьим. Хуже того, вернуть владельца
        // потом нечем: Show(owner) на уже видимом окне — no-op. Без владельца CenterOwner молча
        // откатывается на CenterScreen, а оконный менеджер Linux перестаёт держать служебное окно
        // поверх главного (transient-for). Повторный Show(owner) дубля в списке детей не создаёт.
        window.Show(owner);
        window.Activate();
        return window;
    }
}
