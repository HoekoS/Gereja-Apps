// src/ChurchProjection.Api/Access/PairGate.cs
using ChurchProjection.Api.Options;

using Microsoft.Extensions.Options;

namespace ChurchProjection.Api.Access;

public static class PairGate
{
    /// <summary>
    /// Applied to every route except health, pair, and the output hub role.
    /// A filter rather than middleware so the exemptions are visible at the
    /// route that has them, instead of in a path list somewhere else.
    /// </summary>
    public static TBuilder RequirePair<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;

            if (await IsPairedAsync(http))
            {
                return await next(context);
            }

            return ApiError.Result(401, "NOT_PAIRED", "Enter the PIN shown on the booth screen.");
        });

        return builder;
    }

    public static async Task<bool> IsPairedAsync(HttpContext http)
    {
        var access = http.RequestServices.GetRequiredService<IOptions<AccessOptions>>().Value;

        // FR-SEC-08: the booth's own browser never types the PIN. The test
        // suite switches this off so the gate can be observed to reject.
        if (!access.RequirePairingFromLoopback && IsLoopback(http))
        {
            return true;
        }

        var pin = http.RequestServices.GetRequiredService<PinService>();
        var (_, rotatedAt) = await pin.CurrentAsync(http.RequestAborted);

        return PairTicket.IsValid(http, rotatedAt);
    }

    public static bool IsLoopback(HttpContext http) =>
        http.Connection.RemoteIpAddress is { } address && System.Net.IPAddress.IsLoopback(address);
}
