using System;
using Vantah.Core.Errors;

namespace Vantah.App.Localization;

/// <summary>
/// Сообщение, которое вьюмодель хранит значением, а не готовой строкой: текст собирается
/// на текущем языке в момент показа. Поэтому переключение языка перерисовывает уже висящее
/// на экране сообщение, а не оставляет его на языке, который был в момент показа.
/// </summary>
public readonly record struct UiText
{
    private readonly AppError? _error;   // причина из ядра
    private readonly string? _key;       // либо строка самого UI по ключу ресурсов
    private readonly object[]? _args;

    private UiText(AppError? error, string? key, object[]? args)
    {
        _error = error;
        _key = key;
        _args = args;
    }

    /// <summary>Показывать нечего.</summary>
    public static UiText None => default;

    /// <summary>Причина из ядра.</summary>
    public static UiText Of(AppError error) => new(error, null, null);

    /// <summary>Собственная строка UI по ключу ресурсов (например, «это не похоже на домен»).</summary>
    public static UiText Key(string locKey, params object[] args) => new(null, locKey, args);

    /// <summary>
    /// Исключение из ядра. Переводимую причину берём из <see cref="IAppErrorException"/>;
    /// у чужих исключений (IO, сеть, .NET) переводить нечего — показываем их текст.
    /// </summary>
    public static UiText From(Exception ex) => ex switch
    {
        IAppErrorException e            => Of(e.Error),
        OperationCanceledException      => Key(LocKeys.Error_Cancelled),
        _                               => Of(AppError.Cli(ex.Message)),
    };

    public bool HasValue => _error is not null || _key is not null;

    /// <summary>Текст на текущем языке интерфейса; null — показывать нечего.</summary>
    public string? Text
    {
        get
        {
            var loc = Localizer.Instance;
            if (_error is { } e)
                return e.Code == AppErrorCode.CliOutput
                    ? e.Argument                       // готовый текст (CLI, чужое исключение): не переводится
                    : loc.Format(KeyOf(e.Code), e.Argument);
            if (_key is null) return null;
            return _args is { Length: > 0 } a ? loc.Format(_key, a) : loc[_key];
        }
    }

    private static string KeyOf(AppErrorCode code) => code switch
    {
        AppErrorCode.Timeout            => LocKeys.Error_Timeout,
        AppErrorCode.AddDomainFailed    => LocKeys.Error_AddDomainFailed,
        AppErrorCode.RemoveDomainFailed => LocKeys.Error_RemoveDomainFailed,
        AppErrorCode.ModeSwitchFailed   => LocKeys.Error_ModeSwitchFailed,
        AppErrorCode.LoginSucceeded     => LocKeys.Login_Succeeded,
        AppErrorCode.LoginFailed        => LocKeys.Login_Failed,
        AppErrorCode.LoginCancelled     => LocKeys.Login_Cancelled,
        AppErrorCode.LoginTimedOut      => LocKeys.Login_TimedOut,
        _                               => LocKeys.Error_CommandFailed,
    };
}
