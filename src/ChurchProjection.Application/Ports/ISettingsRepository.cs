namespace ChurchProjection.Application.Ports;

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct);

    Task SetAsync(string key, string value, CancellationToken ct);
}
