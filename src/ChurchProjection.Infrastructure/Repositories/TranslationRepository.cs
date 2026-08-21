using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class TranslationRepository(ProjectionDbContext db) : ITranslationRepository
{
    public async Task<IReadOnlyList<Translation>> ListAsync(CancellationToken ct) =>
        await db.Translations.AsNoTracking().OrderBy(t => t.Abbrev).ToListAsync(ct);

    public Task<Translation?> FindAsync(TranslationId id, CancellationToken ct) =>
        db.Translations.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
}
