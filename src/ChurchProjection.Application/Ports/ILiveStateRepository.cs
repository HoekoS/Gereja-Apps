using ChurchProjection.Domain.Live;

namespace ChurchProjection.Application.Ports;

public interface ILiveStateRepository
{
    Task<LiveSnapshot?> LoadAsync(CancellationToken ct);

    Task SaveAsync(LiveSnapshot snapshot, CancellationToken ct);
}
