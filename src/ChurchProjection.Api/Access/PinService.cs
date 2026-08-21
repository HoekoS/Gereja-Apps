using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Access;

namespace ChurchProjection.Api.Access;

/// <summary>
/// Reads the shared PIN, rotating it lazily when the stored timestamp is older
/// than the most recent Saturday midnight (FR-SEC-03). Lazily, because a weekly
/// scheduler on a machine that is switched off six days a week rotates nothing.
/// </summary>
public sealed class PinService(ISettingsRepository settings, Microsoft.Extensions.Options.IOptions<Options.AccessOptions> access)
{
    private const string PinKey = "pin";
    private const string RotatedAtKey = "pin_rotated_at";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<(string Pin, DateTime RotatedAt)> CurrentAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);

        try
        {
            var stored = await settings.GetAsync(PinKey, ct);
            var rotatedRaw = await settings.GetAsync(RotatedAtKey, ct);
            var rotatedAt = rotatedRaw is null
                ? DateTime.MinValue
                : DateTime.Parse(rotatedRaw, null, System.Globalization.DateTimeStyles.None);

            var now = DateTime.Now;

            if (stored is null || PinRotation.ShouldRotate(rotatedAt, now))
            {
                stored = access.Value.TestPin ?? Pin.Generate().Value;
                rotatedAt = now;

                await settings.SetAsync(PinKey, stored, ct);
                await settings.SetAsync(RotatedAtKey, rotatedAt.ToString("o"), ct);
            }

            return (stored, rotatedAt);
        }
        finally
        {
            Gate.Release();
        }
    }
}
