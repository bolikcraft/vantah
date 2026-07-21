using Vantah.Core.Update;

namespace Vantah.Core.Tests.Fakes;

/// <summary>Источник релизов без сети: отдаёт заданный результат и считает обращения.</summary>
public sealed class FakeAppReleaseSource(AppUpdateInfo? result = null) : IAppReleaseSource
{
    public int Calls { get; private set; }

    public Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(result);
    }
}
