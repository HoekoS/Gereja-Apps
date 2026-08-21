// src/ChurchProjection.Api/Live/LiveStateDto.cs
using ChurchProjection.Application.Live;

namespace ChurchProjection.Api.Live;

public sealed record SlotDto(string ItemId, int PageIndex, object? Content);

public sealed record LiveStateDto(
    string? ServiceId,
    SlotDto? Live,
    SlotDto? Preview,
    bool Blackout,
    IReadOnlyList<string> Skipped,
    int OutputsConnected)
{
    public static LiveStateDto From(LiveView view, int outputsConnected) => new(
        view.Snapshot.ServiceId,
        view.Snapshot.Live is { } live ? new SlotDto(live.ItemId.Value, live.PageIndex, view.LiveContent) : null,
        view.Snapshot.Preview is { } preview
            ? new SlotDto(preview.ItemId.Value, preview.PageIndex, view.PreviewContent)
            : null,
        view.Snapshot.Blackout,
        [.. view.Snapshot.Skipped.Select(id => id.Value)],
        outputsConnected);
}
