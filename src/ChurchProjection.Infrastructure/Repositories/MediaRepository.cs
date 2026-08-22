using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Persistence;
using ChurchProjection.Infrastructure.Storage;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

/// <summary>
/// The media root is passed in rather than stored per row: a row records the
/// filename and nothing else, so a database restored onto a machine whose media
/// folder sits somewhere else still finds its files.
/// </summary>
public sealed class MediaRepository(ProjectionDbContext db, string mediaRoot) : IMediaRepository
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

    public async Task<bool> IsAvailableAsync(MediaId id, CancellationToken ct) =>
        await FindAsync(id, ct) is { } item && MediaPaths.Exists(mediaRoot, item.Filename);
}
