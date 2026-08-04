using System;
using Avalonia.Controls;
using Vantah.App.Localization;

namespace Vantah.App.Views;

/// <summary>
/// Служебное окно: заголовок и слот контента. Закрытие крестиком не разрушает окно, а прячет —
/// контент (вью со своей вьюмоделью) остаётся подключённым и не теряет состояние.
/// </summary>
public sealed class DialogWindow : Window
{
    /// <param name="title">
    /// Фабрика заголовка, а не готовая строка: язык интерфейса переключают внутри одного из этих
    /// самых окон («Настройки»), поэтому заголовок обязан переехать на новый язык сразу, а не
    /// дожидаться закрытия и переоткрытия.
    /// </param>
    public DialogWindow(Func<string> title, Control content, double width = 560, double height = 720)
    {
        Title = title();
        Content = content;
        Width = width;
        Height = height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Отписки нет намеренно: окон конечное число (по одному на пункт меню), закрытие их не
        // разрушает, и живут они ровно столько же, сколько синглтон Localizer, — течь нечему.
        Localizer.Instance.LanguageChanged += (_, _) => Title = title();

        // Только закрытие пользователем прячет окно. Завершение сеанса
        // (ApplicationShutdown/OSShutdown) обязано закрыть его по-настоящему: Avalonia участвует
        // в X11-сессии (XSMP), и вето на закрытие менеджер сеанса понимает как отмену выключения.
        Closing += (_, e) =>
        {
            if (e.CloseReason != WindowCloseReason.WindowClosing) return;
            e.Cancel = true;
            Hide();
        };
    }
}
