using Vantah.Core.Models;

namespace Vantah.Core.Exclusions;

public interface IExclusionsService
{
    Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default);
    Task AddAsync(string domain, CancellationToken ct = default);
    Task RemoveAsync(string domain, CancellationToken ct = default);
    Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
        IReadOnlyList<string> currentDomains, CancellationToken ct = default);
}
