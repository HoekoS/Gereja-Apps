// src/ChurchProjection.Api/Access/PairTicket.cs
using Microsoft.AspNetCore.DataProtection;

namespace ChurchProjection.Api.Access;

/// <summary>
/// The pair cookie. Its payload is the PIN's rotation timestamp, so rotating the
/// PIN invalidates every ticket issued before it (FR-SEC-06) without keeping a
/// server-side session list.
/// </summary>
public static class PairTicket
{
    public const string CookieName = "pair";

    private const string Purpose = "church-projection.pair.v1";

    public static void Issue(HttpContext context, DateTime rotatedAt)
    {
        var protector = context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

        context.Response.Cookies.Append(CookieName, protector.Protect(rotatedAt.ToString("o")), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,

            // Not Secure: this is plain HTTP on the LAN by design. See the
            // accepted risk in the design document.
            Secure = false,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(7),
        });
    }

    public static bool IsValid(HttpContext context, DateTime rotatedAt)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie) || string.IsNullOrEmpty(cookie))
        {
            return false;
        }

        var protector = context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

        try
        {
            return DateTime.Parse(
                protector.Unprotect(cookie), null, System.Globalization.DateTimeStyles.None) == rotatedAt;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }
}
