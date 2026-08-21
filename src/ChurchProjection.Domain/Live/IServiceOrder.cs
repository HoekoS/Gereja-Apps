namespace ChurchProjection.Domain.Live;

/// <summary>
/// Everything the live aggregate is allowed to know about a service. Three
/// members, deliberately: widen this and the aggregate starts making decisions
/// that belong to the library.
/// </summary>
public interface IServiceOrder
{
    bool Contains(ItemId id);

    int PageCount(ItemId id);

    bool MediaAvailable(ItemId id);
}
