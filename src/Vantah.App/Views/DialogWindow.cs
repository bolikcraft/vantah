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
    public DialogWindow(Func<string> title, Control content)
    {
        Title = title();
        Content = content;
        Width = 560;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Отписки нет намеренно: окон конечное число (по одному на пункт меню), закрытие их не
        // разрушает, и живут они ровно столько же, сколько синглтон Localizer, — течь нечему.
        Localizer.Instance.LanguageChanged += (_, _) => Title = title();

        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }
}
