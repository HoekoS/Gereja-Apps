namespace ChurchProjection.Domain.Live;

/// <summary>
/// The single authority on what is on the screen. Held in memory by the
/// server, persisted after every change, and broadcast whole. Refusals are
/// returned, never thrown: an operator pressing a key that cannot apply right
/// now is an ordinary Sunday, not an error condition.
/// </summary>
public sealed class LiveSession
{
    private readonly List<ItemId> _skipped = [];

    public Slot? Live { get; private set; }

    public Slot? Preview { get; private set; }

    public bool Blackout { get; private set; }

    public string? ServiceId { get; private set; }

    public IReadOnlyCollection<ItemId> Skipped => _skipped;

    public static LiveSession New() => new();

    public static LiveSession Restore(LiveSnapshot snapshot)
    {
        var session = new LiveSession
        {
            Live = snapshot.Live,
            Preview = snapshot.Preview,
            Blackout = snapshot.Blackout,
            ServiceId = snapshot.ServiceId,
        };

        session._skipped.AddRange(snapshot.Skipped);

        return session;
    }

    public void AttachService(string? serviceId) => ServiceId = serviceId;

    /// <summary>
    /// A copy. The skipped list is materialised rather than handed out live,
    /// so a snapshot taken before a command still reads the same afterwards.
    /// </summary>
    public LiveSnapshot Snapshot() =>
        new(Live, Preview, Blackout, [.. _skipped], ServiceId);

    public LiveResult PreviewItem(ItemId id, int pageIndex, IServiceOrder order)
    {
        if (!order.Contains(id))
        {
            return LiveResult.Refuse(RefusalCode.UnknownItem);
        }

        if (pageIndex < 0 || pageIndex >= order.PageCount(id))
        {
            // A control view that still thinks the song has six pages after it
            // was re-imported with four. Refuse rather than clamp: the operator
            // needs to know their screen is stale.
            return LiveResult.Refuse(RefusalCode.PageOutOfRange);
        }

        Preview = new Slot(id, pageIndex, order.MediaAvailable(id));

        return LiveResult.Ok;
    }

    public LiveResult Go()
    {
        if (Preview is not { } staged)
        {
            return LiveResult.Refuse(RefusalCode.NoPreview);
        }

        if (!staged.MediaAvailable)
        {
            return LiveResult.Refuse(RefusalCode.MediaUnavailable);
        }

        Live = staged;
        Preview = null;

        return LiveResult.Ok;
    }

    public LiveResult Advance(IServiceOrder order)
    {
        if (Live is not { } live)
        {
            return LiveResult.Refuse(RefusalCode.NoLiveItem);
        }

        var lastPage = order.PageCount(live.ItemId) - 1;

        // Holding on the last page is not an error. The operator holds the key
        // down at the end of a chorus; the screen must simply stay put.
        Live = live with { PageIndex = Math.Min(live.PageIndex + 1, lastPage) };

        return LiveResult.Ok;
    }

    public LiveResult Back()
    {
        if (Live is not { } live)
        {
            return LiveResult.Refuse(RefusalCode.NoLiveItem);
        }

        Live = live with { PageIndex = Math.Max(live.PageIndex - 1, 0) };

        return LiveResult.Ok;
    }

    public LiveResult SetBlackout(bool on)
    {
        Blackout = on;

        return LiveResult.Ok;
    }

    /// <summary>
    /// Clears what is live. Preview is left staged on purpose: clearing the
    /// screen is not the same as abandoning what comes next.
    /// </summary>
    public LiveResult Clear()
    {
        Live = null;

        return LiveResult.Ok;
    }

    public LiveResult Skip(ItemId id, IServiceOrder order)
    {
        if (!order.Contains(id))
        {
            return LiveResult.Refuse(RefusalCode.UnknownItem);
        }

        if (!_skipped.Contains(id))
        {
            _skipped.Add(id);
        }

        return LiveResult.Ok;
    }

    public LiveResult Unskip(ItemId id)
    {
        _skipped.Remove(id);

        return LiveResult.Ok;
    }
}
