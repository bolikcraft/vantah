using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Vantah.App.Localization;
using Vantah.App.ViewModels;

namespace Vantah.App.Views;

public partial class DomainsView : UserControl
{
    public DomainsView() => InitializeComponent();

    private DomainsViewModel? Vm => DataContext as DomainsViewModel;

    private async void OnPasteClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var text = clipboard is null ? null : await clipboard.TryGetTextAsync();
        if (Vm is { } vm) await vm.PasteCommand.ExecuteAsync(text);
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || Vm is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Localizer.Instance[LocKeys.Domains_ExportDialogTitle],
            SuggestedFileName = "exclusions.vantah",
            DefaultExtension = "vantah",
        });
        if (file?.TryGetLocalPath() is { } path) await Vm.ExportAsync(path);
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || Vm is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Localizer.Instance[LocKeys.Domains_ImportDialogTitle],
            AllowMultiple = false,
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) await Vm.ImportAsync(path);
    }

    private async void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var loc = Localizer.Instance;
        if (await ConfirmAsync(loc[LocKeys.Domains_ClearConfirmTitle], loc[LocKeys.Domains_ClearConfirmMessage]))
            await Vm.ClearCommand.ExecuteAsync(null);
    }

    // Простое модальное подтверждение через Window, без зависимости от темы FluentAvalonia.
    private async Task<bool> ConfirmAsync(string title, string message)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return false;

        var result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var ok = new Button { Content = Localizer.Instance[LocKeys.Common_Clear], IsDefault = true };
        var cancel = new Button { Content = Localizer.Instance[LocKeys.Common_Cancel], IsCancel = true };
        ok.Click += (_, _) => { result = true; dialog.Close(); };
        cancel.Click += (_, _) => { result = false; dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
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
