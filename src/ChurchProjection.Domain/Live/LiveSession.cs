namespace ChurchProjection.Domain.Live;

public sealed class LiveSession
{
    public Slot? Live { get; private set; }

    public Slot? Preview { get; private set; }

    public bool Blackout { get; private set; }

    public IReadOnlyCollection<ItemId> Skipped => throw new NotImplementedException();

    public static LiveSession New() => throw new NotImplementedException();

    public LiveSnapshot Snapshot() => throw new NotImplementedException();

    public LiveResult PreviewItem(ItemId id, int pageIndex, IServiceOrder order) =>
        throw new NotImplementedException();

    public LiveResult Go() => throw new NotImplementedException();

    public LiveResult Advance(IServiceOrder order) => throw new NotImplementedException();

    public LiveResult Back() => throw new NotImplementedException();

    public LiveResult SetBlackout(bool on) => throw new NotImplementedException();

    public LiveResult Clear() => throw new NotImplementedException();

    public LiveResult Skip(ItemId id, IServiceOrder order) => throw new NotImplementedException();

    public LiveResult Unskip(ItemId id) => throw new NotImplementedException();
}
