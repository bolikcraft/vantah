using System;
using Avalonia.Controls;
using Vantah.App.ViewModels;

namespace Vantah.App.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel vm)
            {
                // Пароль отдаём как char[] прямо из поля — во вьюмодели он не хранится строкой.
                vm.PasswordProvider = () => PasswordBox.Text?.ToCharArray() ?? Array.Empty<char>();
                // После успешного входа очищаем поле пароля.
                vm.PasswordCleared = () => PasswordBox.Text = "";
            }
        };
    }
}
