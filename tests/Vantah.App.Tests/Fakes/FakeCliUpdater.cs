using Vantah.Core.Update;

namespace Vantah.App.Tests.Fakes;

public sealed class FakeCliUpdater(UpdateResult? result = null) : ICliUpdater
{
    private readonly UpdateResult _result = result ?? new UpdateResult(UpdateOutcome.Updated, "Update completed");
    public int Calls { get; private set; }

    public Task<UpdateResult> UpdateAsync(CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(_result);
    }
}
