namespace Vantah.Core.Update;

/// <summary>Итог проверки обновления adguardvpn-cli.</summary>
public sealed record UpdateStatus(bool IsLatest, string? LatestVersion);
