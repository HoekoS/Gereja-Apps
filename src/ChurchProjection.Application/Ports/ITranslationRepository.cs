using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Ports;

public interface ITranslationRepository
{
    Task<IReadOnlyList<Translation>> ListAsync(CancellationToken ct);

    Task<Translation?> FindAsync(TranslationId id, CancellationToken ct);
}
