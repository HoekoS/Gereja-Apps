namespace ChurchProjection.Application.Ports;

/// <summary>
/// A transaction belongs to the use case that needs one, not to a repository.
/// Only the import uses this: it is the only operation where a half-written
/// result is worse than no result.
/// </summary>
public interface IUnitOfWork
{
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
