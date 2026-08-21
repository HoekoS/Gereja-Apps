using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Live;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Application.Ports;

public sealed record ServiceSummary(string Id, string Name, DateOnly ServiceDate, int ItemCount);

public interface IServiceRepository
{
    Task<ServicePlan?> FindAsync(ServiceId id, CancellationToken ct);

    /// <summary>
    /// The service that holds this item. The live state attaches itself to a
    /// service when the operator previews something in it, so there is no separate
    /// "open the service" step to forget.
    /// </summary>
    Task<ServicePlan?> FindByItemAsync(ItemId itemId, CancellationToken ct);

    Task<IReadOnlyList<ServiceSummary>> ListAsync(CancellationToken ct);

    /// <summary>Saves the whole aggregate, items included. Positions are the
    /// aggregate's business, so there is no per-item write.</summary>
    Task SaveAsync(ServicePlan plan, CancellationToken ct);

    Task RemoveAsync(ServiceId id, CancellationToken ct);
}
