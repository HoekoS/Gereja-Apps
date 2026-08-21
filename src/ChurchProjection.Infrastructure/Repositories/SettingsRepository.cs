using ChurchProjection.Application.Ports;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class SettingsRepository(ProjectionDbContext db) : ISettingsRepository
{
    public async Task<string?> GetAsync(string key, CancellationToken ct) =>
        (await db.Settings.AsNoTracking().SingleOrDefaultAsync(s => s.Key == key, ct))?.Value;

    public async Task SetAsync(string key, string value, CancellationToken ct)
    {
        var existing = await db.Settings.SingleOrDefaultAsync(s => s.Key == key, ct);

        if (existing is null)
        {
            db.Settings.Add(new SettingRow { Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
        }

        await db.SaveChangesAsync(ct);
    }
}
