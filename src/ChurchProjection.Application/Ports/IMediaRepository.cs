using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Ports;

public interface IMediaRepository
{
    Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct);

    Task<MediaItem?> FindAsync(MediaId id, CancellationToken ct);

    Task<MediaId> AddAsync(MediaItem item, CancellationToken ct);

    Task RemoveAsync(MediaId id, CancellationToken ct);
}
