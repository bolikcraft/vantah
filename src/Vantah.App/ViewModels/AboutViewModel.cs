using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.App.Localization;
using Vantah.App.Services;
using Vantah.Core.Update;
using Vantah.Core.Vpn;

namespace Vantah.App.ViewModels;

public partial class AboutViewModel : ObservableObject, IReloadableSection
{
    public const string RepositoryUrl = "https://github.com/bolikcraft/vantah";

    private const string DefaultVersion = "1.0.0";

    private readonly IVpnService _vpn;

    // Пока версия CLI не получена, в поле стоит локализованная заглушка; ключ помним,
    // чтобы после смены языка перевести её (у реальной версии ключа нет — её не трогаем).
    private string? _cliVersionKey = LocKeys.Common_Loading;

    [ObservableProperty] private string _cliVersion = Localizer.Instance[LocKeys.Common_Loading];

    // Релиз новее текущей версии; null — обновления нет и ссылку не показываем.
    private AppUpdateInfo? _update;

    [ObservableProperty] private bool _hasUpdate;
    [ObservableProperty] private string _updateText = "";
    [ObservableProperty] private Uri? _updateUri;

    public AboutViewModel(IVpnService vpn)
    {
        _vpn = vpn;
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(LegalNotice));
            if (_cliVersionKey is { } key) CliVersion = Localizer.Instance[key];
            RefreshUpdateText();
        });
        LoadTask = LoadCliVersionAsync(CancellationToken.None);
    }

    /// <summary>Текущее (повторное) чтение версии CLI — чтобы его можно было дождаться в тестах.</summary>
    public Task LoadTask { get; private set; }

    /// <inheritdoc/>
    public string Id => "about";

    /// <inheritdoc/>
    public bool LoadFailed => _cliVersionKey == LocKeys.Common_NotAvailable;

    /// <inheritdoc/>
    public Task ReloadIfFailedAsync() =>
        LoadFailed ? LoadTask = LoadCliVersionAsync(CancellationToken.None) : Task.CompletedTask;

    /// <summary>Версия сборки — общая для экрана «О программе» и проверки обновлений.</summary>
    public static string CurrentVersion { get; } = ReadAppVersion();

    public string AppVersion => CurrentVersion;

    public string Repository => RepositoryUrl;

    public Uri RepositoryUri { get; } = new(RepositoryUrl);

    public string LegalNotice => Localizer.Instance[LocKeys.About_LegalNotice];

    /// <summary>Показать ссылку на более новый релиз; null — убрать её. Вызывать на UI-потоке.</summary>
    public void ShowUpdate(AppUpdateInfo? info)
    {
        _update = info;
        UpdateUri = info?.ReleasePageUri();
        RefreshUpdateText();
        HasUpdate = info is not null;
    }

    private void RefreshUpdateText() =>
        UpdateText = _update is null
            ? ""
            : Localizer.Instance.Format(LocKeys.Update_Available, _update.Version.TrimStart('v', 'V'));

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
