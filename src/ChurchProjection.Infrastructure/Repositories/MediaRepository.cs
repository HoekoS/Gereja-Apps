using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class MediaRepository(ProjectionDbContext db) : IMediaRepository
{
    public async Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct) =>
        await db.Media.AsNoTracking().OrderBy(m => m.Filename).ToListAsync(ct);

    public Task<MediaItem?> FindAsync(MediaId id, CancellationToken ct) =>
        db.Media.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);

    public async Task<MediaId> AddAsync(MediaItem item, CancellationToken ct)
    {
        db.Media.Add(item);
        await db.SaveChangesAsync(ct);

        return item.Id;
    }

    public async Task RemoveAsync(MediaId id, CancellationToken ct)
    {
        await db.Media.Where(m => m.Id == id).ExecuteDeleteAsync(ct);
    }
}
