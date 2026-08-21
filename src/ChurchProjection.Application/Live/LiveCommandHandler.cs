// src/ChurchProjection.Application/Live/LiveCommandHandler.cs
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Live;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Application.Live;

public sealed record LiveView(LiveSnapshot Snapshot, object? LiveContent, object? PreviewContent);

/// <summary>
/// Loads the session, applies one command, persists the result. Every command
/// path goes through here, so "the server decides" is a single method rather
/// than a rule the transports are trusted to follow.
/// </summary>
public sealed class LiveCommandHandler(
    ILiveStateRepository state,
    IServiceRepository services,
    ContentResolver content)
{
    public async Task<LiveView> CurrentAsync(CancellationToken ct)
    {
        var snapshot = await state.LoadAsync(ct) ?? LiveSession.New().Snapshot();

        return await DescribeAsync(snapshot, ct);
    }

    public async Task<(LiveResult Result, LiveView View)> ExecuteAsync(LiveCommand command, CancellationToken ct)
    {
        var snapshot = await state.LoadAsync(ct) ?? LiveSession.New().Snapshot();
        var session = LiveSession.Restore(snapshot);

        // preview is the only command that names a new item, so it is also the
        // only one that can change which service is live. Attaching here means
        // the operator never has to "open" a service first.
        var order = await LoadOrderAsync(session, command, ct);

        var result = command.Type switch
        {
            "preview" when command.ItemId is { } id =>
                session.PreviewItem(new ItemId(id), command.PageIndex ?? 0, order),
            "go" => session.Go(),
            "advance" => session.Advance(order),
            "back" => session.Back(),
            "blackout" => session.SetBlackout(command.On ?? true),
            "clear" => session.Clear(),
            "skip" when command.ItemId is { } id => session.Skip(new ItemId(id), order),
            "unskip" when command.ItemId is { } id => session.Unskip(new ItemId(id)),
            _ => LiveResult.Refuse(RefusalCode.UnknownCommand),
        };

        if (result.IsOk)
        {
            await state.SaveAsync(session.Snapshot(), ct);
        }

        return (result, await DescribeAsync(session.Snapshot(), ct));
    }

    private async Task<IServiceOrder> LoadOrderAsync(
        LiveSession session, LiveCommand command, CancellationToken ct)
    {
        ServicePlan? plan = null;

        if (command.Type == "preview" && command.ItemId is { } itemId)
        {
            plan = await services.FindByItemAsync(new ItemId(itemId), ct);

            if (plan is not null)
            {
                session.AttachService(plan.Id.Value);
            }
        }

        plan ??= session.Snapshot().ServiceId is { } serviceId
            ? await services.FindAsync(new ServiceId(serviceId), ct)
            : null;

        if (plan is null)
        {
            return EmptyOrder.Instance;
        }

        var counts = new Dictionary<string, int>();

        foreach (var item in plan.Items)
        {
            counts[item.Id] = await content.PageCountAsync(item, ct);
        }

        return new ServiceOrderView(plan, counts)
        {
            Unavailable = await content.UnavailableAsync(plan, ct),
        };
    }

    private async Task<LiveView> DescribeAsync(LiveSnapshot snapshot, CancellationToken ct)
    {
        var plan = snapshot.ServiceId is { } serviceId
            ? await services.FindAsync(new ServiceId(serviceId), ct)
            : null;

        return new LiveView(
            snapshot,
            await ResolveAsync(plan, snapshot.Live, ct),
            await ResolveAsync(plan, snapshot.Preview, ct));
    }

    private async Task<object?> ResolveAsync(ServicePlan? plan, Slot? slot, CancellationToken ct) =>
        plan is null || slot is null || plan.Find(slot.ItemId.Value) is not { } item
            ? null
            : await content.ResolveAsync(item, slot.PageIndex, ct);
}
