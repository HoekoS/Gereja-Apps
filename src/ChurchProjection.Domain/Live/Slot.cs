namespace ChurchProjection.Domain.Live;

/// <summary>
/// What is on a screen. MediaAvailable is captured when the slot is staged, so
/// Go() can refuse without asking the service order again.
/// </summary>
public sealed record Slot(ItemId ItemId, int PageIndex, bool MediaAvailable);
