using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
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
        if (await ConfirmDialog.ShowAsync(this,
                loc[LocKeys.Domains_ClearConfirmTitle], loc[LocKeys.Domains_ClearConfirmMessage],
                loc[LocKeys.Common_Clear], loc[LocKeys.Common_Cancel]))
            await Vm.ClearCommand.ExecuteAsync(null);
    }
}
