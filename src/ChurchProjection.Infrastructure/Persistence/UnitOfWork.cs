using ChurchProjection.Application.Ports;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Persistence;

public sealed class UnitOfWork(ProjectionDbContext db) : IUnitOfWork
{
    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var result = await work(ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return result;
    }
}
