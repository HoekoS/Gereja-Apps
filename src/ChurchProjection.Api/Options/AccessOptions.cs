namespace ChurchProjection.Api.Options;

public sealed class AccessOptions
{
    public const string Section = "Access";

    /// <summary>
    /// Test-only. Pins the PIN so the API suite does not have to read it from
    /// the loopback-only endpoint. Refused in Production.
    /// </summary>
    public string? TestPin { get; set; }

    /// <summary>
    /// Test-only. Switches off the loopback exemption so the pair gate can be
    /// observed to reject. Refused in Production.
    /// </summary>
    public bool RequirePairingFromLoopback { get; set; }

    /// <summary>Pair attempts allowed per remote address per window (NFR-SEC-05).</summary>
    public int PairAttemptsPerWindow { get; set; } = 5;

    public TimeSpan PairWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Pair attempts allowed across every address together (NFR-SEC-05). The
    /// per-address limit alone bounds nothing an attacker cares about, because
    /// one laptop can bind a dozen addresses. 60 an hour is 10,080 a week
    /// against a million-value PIN, or 1% of an exhaustive search, and far more
    /// than a church LAN pairs in an hour.
    /// </summary>
    public int PairAttemptsPerGlobalWindow { get; set; } = 60;

    public TimeSpan PairGlobalWindow { get; set; } = TimeSpan.FromHours(1);
}
