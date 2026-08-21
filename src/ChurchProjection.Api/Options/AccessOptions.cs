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
}
