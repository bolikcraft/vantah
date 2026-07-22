using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Vantah.App.Localization;

namespace Vantah.App.ViewModels;

/// <summary>
/// Вьюмодель с сообщением об ошибке на экране. Хранит причину (<see cref="UiText"/>), а не
/// готовую строку, и пересобирает <see cref="Error"/> при смене языка — иначе сообщение
/// оставалось бы на языке, который был в момент сбоя.
/// </summary>
public abstract partial class ErrorAwareViewModel : ObservableObject
{
    private UiText _errorValue;

    /// <summary>Текст ошибки на текущем языке; null — ошибки нет. Сюда привязан XAML.</summary>
    [ObservableProperty] private string? _error;

    protected ErrorAwareViewModel()
    {
        Localizer.Instance.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(RefreshError);
    }

    /// <summary>Есть ли что показывать (в отличие от <see cref="Error"/>, не зависит от языка).</summary>
    protected bool HasError => _errorValue.HasValue;

    protected void SetError(UiText value)
    {
        _errorValue = value;
        RefreshError();
    }

    protected void SetError(Exception ex) => SetError(UiText.From(ex));

    /// <summary>Ошибка UI по ключу ресурсов (не из ядра).</summary>
    protected void SetError(string locKey, params object[] args) => SetError(UiText.Key(locKey, args));

    protected void ClearError() => SetError(UiText.None);

    private void RefreshError() => Error = _errorValue.Text;
}
