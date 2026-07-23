namespace Vantah.Core.Update;

public interface ICliUpdater
{
    Task<UpdateResult> UpdateAsync(CancellationToken ct = default);
}
