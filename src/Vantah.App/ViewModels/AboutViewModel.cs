using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.App.Localization;
using Vantah.Core.Vpn;

namespace Vantah.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public const string RepositoryUrl = "https://gitlab.com/bolikcraft/vantah";

    private const string DefaultVersion = "1.0.0";

    private readonly IVpnService _vpn;

    // Пока версия CLI не получена, в поле стоит локализованная заглушка; ключ помним,
    // чтобы после смены языка перевести её (у реальной версии ключа нет — её не трогаем).
    private string? _cliVersionKey = LocKeys.Common_Loading;

    [ObservableProperty] private string _cliVersion = Localizer.Instance[LocKeys.Common_Loading];

    public AboutViewModel(IVpnService vpn)
    {
        _vpn = vpn;
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(LegalNotice));
            if (_cliVersionKey is { } key) CliVersion = Localizer.Instance[key];
        });
        _ = LoadCliVersionAsync(CancellationToken.None);
    }

    public string AppVersion { get; } = ReadAppVersion();

    public string Repository => RepositoryUrl;

    public Uri RepositoryUri { get; } = new(RepositoryUrl);

    public string LegalNotice => Localizer.Instance[LocKeys.About_LegalNotice];

    private async Task LoadCliVersionAsync(CancellationToken ct)
    {
        try
        {
            var version = await _vpn.GetCliVersionAsync(ct);
            SetCliVersion(version);
        }
        catch
        {
            SetCliVersion(null);
        }
    }

    private void SetCliVersion(string? version)
    {
        _cliVersionKey = version is null ? LocKeys.Common_NotAvailable : null;
        CliVersion = version ?? Localizer.Instance[LocKeys.Common_NotAvailable];
    }

    /// <summary>
    /// Версия из AssemblyInformationalVersion без суффикса сборки (+хеш коммита).
    /// </summary>
    private static string ReadAppVersion()
    {
        var informational = typeof(AboutViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational)) return DefaultVersion;

        var plus = informational.IndexOf('+');
        var version = plus >= 0 ? informational[..plus] : informational;
        return string.IsNullOrWhiteSpace(version) ? DefaultVersion : version;
    }
}
