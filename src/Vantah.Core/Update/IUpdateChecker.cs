namespace Vantah.Core.Update;

public interface IUpdateChecker
{
    Task<UpdateStatus> CheckAsync(CancellationToken ct = default);
}
