using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Ports;

public interface IMediaRepository
{
    Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct);

    Task<MediaItem?> FindAsync(MediaId id, CancellationToken ct);

    Task<MediaId> AddAsync(MediaItem item, CancellationToken ct);

    Task RemoveAsync(MediaId id, CancellationToken ct);

    /// <summary>Whether the file behind this row is on disk right now (FR-LIV-17).</summary>
    Task<bool> IsAvailableAsync(MediaId id, CancellationToken ct);
}
