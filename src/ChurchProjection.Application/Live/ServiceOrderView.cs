// src/ChurchProjection.Application/Live/ServiceOrderView.cs
using ChurchProjection.Domain.Live;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Application.Live;

/// <summary>
/// The Domain's read-only window onto a saved service. LiveSession asks it three
/// questions and nothing more, which is what keeps the aggregate testable
/// without a database (UNT-LIV-19 enforces the shape by reflection).
/// </summary>
public sealed class ServiceOrderView(ServicePlan plan, IReadOnlyDictionary<string, int> pageCounts) : IServiceOrder
{
    public bool Contains(ItemId itemId) => plan.Find(itemId.Value) is not null;

    public bool MediaAvailable(ItemId itemId) => !Unavailable.Contains(itemId.Value);

    public int PageCount(ItemId itemId) =>
        pageCounts.TryGetValue(itemId.Value, out var count) ? count : 1;

    /// <summary>Item ids whose media file was missing when the order was built.</summary>
    public required IReadOnlySet<string> Unavailable { get; init; }

    public ServicePlan Plan => plan;
}
