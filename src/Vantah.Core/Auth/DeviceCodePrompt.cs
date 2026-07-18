namespace Vantah.Core.Auth;

/// <summary>Приглашение device-code входа: ссылка авторизации в браузере, код и время жизни.</summary>
public sealed record DeviceCodePrompt(string Url, string? UserCode, int? ExpiresInSeconds);
