namespace Vantah.Core.Update;

/// <summary>Источник сведений о последнем релизе Vantah. null — узнать не удалось.</summary>
public interface IAppReleaseSource
{
    Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default);
}
