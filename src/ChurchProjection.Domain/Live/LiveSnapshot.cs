namespace ChurchProjection.Domain.Live;

/// <summary>
/// The whole live state, flat and copyable. This is what gets persisted and
/// what gets broadcast; there is no delta form anywhere in the system.
/// </summary>
public sealed record LiveSnapshot(
    Slot? Live,
    Slot? Preview,
    bool Blackout,
    IReadOnlyList<ItemId> Skipped,
    string? ServiceId);
