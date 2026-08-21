namespace ChurchProjection.Domain.Access;

/// <summary>
/// The PIN is good for one week and turns over at Saturday midnight, local
/// wall clock, so the number handed out at Saturday rehearsal is the number
/// that works on Sunday morning.
/// </summary>
public static class PinRotation
{
    /// <summary>
    /// True when a Saturday midnight has passed since the PIN was last set.
    /// Evaluated on demand: nothing schedules this, so a server that was off
    /// for a month rotates exactly once when it comes back up.
    /// </summary>
    public static bool ShouldRotate(DateTime lastRotatedAt, DateTime now) =>
        MostRecentSaturdayMidnight(now) > lastRotatedAt;

    private static DateTime MostRecentSaturdayMidnight(DateTime now)
    {
        var daysSinceSaturday = ((int)now.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;

        return now.Date.AddDays(-daysSinceSaturday);
    }
}
