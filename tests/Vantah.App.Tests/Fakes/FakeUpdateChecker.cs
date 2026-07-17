using Vantah.Core.Update;

namespace Vantah.App.Tests.Fakes;

public sealed class FakeUpdateChecker(UpdateStatus? status = null) : IUpdateChecker
{
    private readonly UpdateStatus _status = status ?? new UpdateStatus(true, null);
    public Task<UpdateStatus> CheckAsync(CancellationToken ct = default) => Task.FromResult(_status);
}
