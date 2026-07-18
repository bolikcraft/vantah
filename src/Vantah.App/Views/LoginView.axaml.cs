using Avalonia.Controls;

namespace Vantah.App.Views;

public partial class LoginView : UserControl
{
    // Открытие браузера ставит App.axaml.cs на LoginViewModel.BrowserOpener (через Launcher окна),
    // здесь ничего дополнительно провязывать не нужно.
    public LoginView() => InitializeComponent();
}
