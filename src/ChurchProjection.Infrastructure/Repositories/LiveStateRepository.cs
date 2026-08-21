using System.Text.Json;

using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Live;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class LiveStateRepository(ProjectionDbContext db) : ILiveStateRepository
{
    private const int SingleRowId = 1;

    public async Task<LiveSnapshot?> LoadAsync(CancellationToken ct)
    {
        var row = await db.LiveState.AsNoTracking().SingleOrDefaultAsync(l => l.Id == SingleRowId, ct);

        if (row is null)
        {
            return null;
        }

        var skipped = JsonSerializer.Deserialize<List<string>>(row.SkippedJson) ?? [];

        return new LiveSnapshot(
            row.LiveItemId is null ? null : new Slot(row.LiveItemId, row.LivePageIndex, row.LiveMediaAvailable),
            row.PreviewItemId is null ? null : new Slot(row.PreviewItemId, row.PreviewPageIndex, row.PreviewMediaAvailable),
            row.Blackout,
            [.. skipped.Select(id => new ItemId(id))],
            row.ServiceId);
    }

    public async Task SaveAsync(LiveSnapshot snapshot, CancellationToken ct)
    {
        var row = await db.LiveState.SingleOrDefaultAsync(l => l.Id == SingleRowId, ct);

        if (row is null)
        {
            row = new LiveStateRow { Id = SingleRowId, SkippedJson = "[]" };
            db.LiveState.Add(row);
        }

        row.ServiceId = snapshot.ServiceId;
        row.LiveItemId = snapshot.Live?.ItemId.Value;
        row.LivePageIndex = snapshot.Live?.PageIndex ?? 0;
        row.LiveMediaAvailable = snapshot.Live?.MediaAvailable ?? false;
        row.PreviewItemId = snapshot.Preview?.ItemId.Value;
        row.PreviewPageIndex = snapshot.Preview?.PageIndex ?? 0;
        row.PreviewMediaAvailable = snapshot.Preview?.MediaAvailable ?? false;
        row.Blackout = snapshot.Blackout;
        row.SkippedJson = JsonSerializer.Serialize(snapshot.Skipped.Select(id => id.Value));
        row.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
