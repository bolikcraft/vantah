using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.Core.Vpn;

namespace Vantah.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public const string RepositoryUrl = "https://gitlab.com/bolikcraft/vantah";

    private const string DefaultVersion = "1.0.0";

    private readonly IVpnService _vpn;

    [ObservableProperty] private string _cliVersion = "загрузка…";

    public AboutViewModel(IVpnService vpn)
    {
        _vpn = vpn;
        _ = LoadCliVersionAsync(CancellationToken.None);
    }

    public string AppVersion { get; } = ReadAppVersion();

    public string Repository => RepositoryUrl;

    public Uri RepositoryUri { get; } = new(RepositoryUrl);

    public string LegalNotice =>
        "Vantah — независимый графический клиент для утилиты adguardvpn-cli. " +
        "Проект не связан с компанией AdGuard, не одобрен и не поддерживается ею. " +
        "«AdGuard» и «AdGuard VPN» — товарные знаки их правообладателей и упоминаются " +
        "только для указания совместимости.";

    private async Task LoadCliVersionAsync(CancellationToken ct)
    {
        try
        {
            CliVersion = await _vpn.GetCliVersionAsync(ct);
        }
        catch
        {
            CliVersion = "недоступно";
        }
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
