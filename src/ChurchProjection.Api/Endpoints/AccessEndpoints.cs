// src/ChurchProjection.Api/Endpoints/AccessEndpoints.cs
using ChurchProjection.Api.Access;

namespace ChurchProjection.Api.Endpoints;

public static class AccessEndpoints
{
    public sealed record PairRequest(string? Pin);

    public static void MapAccess(this WebApplication app)
    {
        app.MapPost("/api/pair", async (PairRequest body, HttpContext http, PinService pins, CancellationToken ct) =>
        {
            var (pin, rotatedAt) = await pins.CurrentAsync(ct);

            if (string.IsNullOrWhiteSpace(body.Pin) || !FixedTimeEquals(body.Pin, pin))
            {
                return ApiError.Result(401, "BAD_PIN", "That PIN is not the one on the booth screen.");
            }

            PairTicket.Issue(http, rotatedAt);

            return Results.NoContent();
        })
        .RequireRateLimiting("pair");

        app.MapGet("/api/pin", async (HttpContext http, PinService pins, CancellationToken ct) =>
        {
            // FR-SEC-09. The PIN is readable only by someone already at the
            // booth machine; from anywhere else this route does not exist as
            // far as the caller is concerned.
            if (!PairGate.IsLoopback(http))
            {
                return ApiError.Result(403, "LOOPBACK_ONLY", "The PIN can only be read on the booth machine.");
            }

            var (pin, rotatedAt) = await pins.CurrentAsync(ct);

            return Results.Json(new { pin, rotatedAt });
        });
    }

    /// <summary>
    /// Constant-time on purpose. The PIN is six digits and the attacker is on
    /// the same LAN; there is no reason to hand them a timing signal as well.
    /// </summary>
    private static bool FixedTimeEquals(string left, string right) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(left), System.Text.Encoding.UTF8.GetBytes(right));
}
