using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Live;
using ChurchProjection.Domain.Services;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class ServiceRepository(ProjectionDbContext db) : IServiceRepository
{
    public Task<ServicePlan?> FindAsync(ServiceId id, CancellationToken ct) =>
        db.Services.SingleOrDefaultAsync(s => s.Id == id, ct);

    public Task<ServicePlan?> FindByItemAsync(ItemId itemId, CancellationToken ct) =>
        db.Services.SingleOrDefaultAsync(s => s.Items.Any(item => item.Id == itemId.Value), ct);

    public async Task<IReadOnlyList<ServiceSummary>> ListAsync(CancellationToken ct) =>
        await db.Services.AsNoTracking()
            .OrderByDescending(s => s.ServiceDate)
            .Select(s => new ServiceSummary(s.Id.Value, s.Name, s.ServiceDate, s.Items.Count))
            .ToListAsync(ct);

    public async Task SaveAsync(ServicePlan plan, CancellationToken ct)
    {
        if (db.Entry(plan).State == EntityState.Detached)
        {
            db.Services.Add(plan);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(ServiceId id, CancellationToken ct)
    {
        var plan = await db.Services.SingleOrDefaultAsync(s => s.Id == id, ct);

        if (plan is null)
        {
            return;
        }

        // Deleting a service deletes its items and nothing else. The song it
        // pointed at stays in the library (FR-SVC-07).
        db.Services.Remove(plan);
        await db.SaveChangesAsync(ct);
    }
}
