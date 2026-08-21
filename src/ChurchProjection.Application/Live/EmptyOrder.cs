// src/ChurchProjection.Application/Live/EmptyOrder.cs
using ChurchProjection.Domain.Live;

namespace ChurchProjection.Application.Live;

/// <summary>
/// The order when nothing is attached. Contains nothing, so preview and skip
/// refuse with UnknownItem; PageCount is 1, so advance holds where it is.
/// </summary>
public sealed class EmptyOrder : IServiceOrder
{
    public static readonly EmptyOrder Instance = new();

    private EmptyOrder()
    {
    }

    public bool Contains(ItemId id) => false;

    public int PageCount(ItemId id) => 1;

    public bool MediaAvailable(ItemId id) => true;
}
