using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Application.Ports;

public sealed record ServiceSummary(string Id, string Name, DateOnly ServiceDate, int ItemCount);

public interface IServiceRepository
{
    Task<ServicePlan?> FindAsync(ServiceId id, CancellationToken ct);

    Task<IReadOnlyList<ServiceSummary>> ListAsync(CancellationToken ct);

    /// <summary>Saves the whole aggregate, items included. Positions are the
    /// aggregate's business, so there is no per-item write.</summary>
    Task SaveAsync(ServicePlan plan, CancellationToken ct);

    Task RemoveAsync(ServiceId id, CancellationToken ct);
}
