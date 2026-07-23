using Avalonia.Controls;
using Avalonia.Interactivity;
using Vantah.App.Localization;
using Vantah.App.ViewModels;

namespace Vantah.App.Views;

public partial class LicenseView : UserControl
{
    public LicenseView() => InitializeComponent();

    // Выход из аккаунта необратим: сначала спрашиваем подтверждение поверх окна аккаунта,
    // и только по «да» дёргаем команду вьюмодели, а затем прячем окно — гейт главного окна
    // сам покажет форму входа, когда зонд координатора увидит разлогин.
    private async void OnLogoutClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LicenseViewModel vm) return;
        var loc = Localizer.Instance;
        if (!await ConfirmDialog.ShowAsync(this,
                loc[LocKeys.Login_LogoutConfirmTitle], loc[LocKeys.Login_LogoutConfirmMessage],
                loc[LocKeys.Login_LogoutConfirmAction], loc[LocKeys.Common_Cancel]))
            return;

        await vm.LogoutCommand.ExecuteAsync(null);
        (TopLevel.GetTopLevel(this) as Window)?.Close();
    }
}
