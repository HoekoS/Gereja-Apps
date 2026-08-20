// Unit tests for PIN generation and weekly rotation (SRS FR-SEC-02 to FR-SEC-04,
// TEST-CASES UNT-PIN-*).
//
// Every timestamp below is a DateTime with Kind=Unspecified, meaning local wall
// clock. The requirement is Saturday 00:00 *local to the booth machine*; testing
// it in UTC would test the wrong thing, and a DateTimeOffset would drag a zone
// into a rule that has none.
//
// RED PHASE: ChurchProjection.Domain does not exist yet.

using ChurchProjection.Domain.Access;

namespace ChurchProjection.Domain.Tests;

public class PinRotationTests
{
    // 2026 calendar anchors used below:
    //   Fri 2026-08-14, Sat 2026-08-15, Sun 2026-08-16,
    //   Fri 2026-08-21, Sat 2026-08-22
    private static DateTime At(int y, int m, int d, int h = 0, int min = 0, int s = 0, int ms = 0) =>
        new(y, m, d, h, min, s, ms, DateTimeKind.Unspecified);

    // Guard the fixture itself: if these are not the weekdays the cases assume,
    // every rotation test below is meaningless.
    [Fact]
    public void Fixture_dates_are_the_weekdays_the_cases_assume()
    {
        Assert.Equal(DayOfWeek.Friday, At(2026, 8, 14).DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, At(2026, 8, 15).DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, At(2026, 8, 22).DayOfWeek);
    }

    // --- generation ---------------------------------------------------------

    [Fact]
    public void UNT_PIN_01_a_pin_is_exactly_six_digits()
    {
        var pin = Pin.Generate();

        Assert.Matches("^[0-9]{6}$", pin.Value);
    }

    [Fact]
    public void UNT_PIN_02_pins_are_not_predictable()
    {
        // Catches a constant, a counter, or a weak seeded generator. A CSPRNG
        // over 10^6 values collides rarely enough that 1000 draws stay well
        // above 900.
        var seen = new HashSet<string>();
        for (var i = 0; i < 1000; i++)
        {
            seen.Add(Pin.Generate().Value);
        }

        Assert.True(seen.Count >= 900, $"expected >= 900 distinct PINs in 1000 draws, got {seen.Count}");
    }

    // --- rotation boundary --------------------------------------------------

    [Fact]
    public void UNT_PIN_03_CRITICAL_crossing_into_saturday_rotates() =>
        Assert.True(PinRotation.ShouldRotate(At(2026, 8, 14, 23, 59), At(2026, 8, 15, 0, 0)));

    [Fact]
    public void UNT_PIN_04_rotating_once_on_saturday_does_not_rotate_again_that_day() =>
        // A second rotation the same weekend would invalidate a device that
        // paired Saturday morning, mid-service on Sunday.
        Assert.False(PinRotation.ShouldRotate(At(2026, 8, 15, 0, 0), At(2026, 8, 15, 12, 0)));

    [Fact]
    public void UNT_PIN_05_a_pin_set_on_saturday_survives_the_following_friday() =>
        Assert.False(PinRotation.ShouldRotate(At(2026, 8, 15, 0, 0), At(2026, 8, 21, 23, 59)));

    [Fact]
    public void UNT_PIN_06_a_pin_set_on_saturday_rotates_the_next_saturday() =>
        Assert.True(PinRotation.ShouldRotate(At(2026, 8, 15, 0, 0), At(2026, 8, 22, 0, 1)));

    [Fact]
    public void UNT_PIN_07_the_boundary_instant_itself_does_not_rerotate()
    {
        var boundary = At(2026, 8, 15, 0, 0, 0, 0);

        Assert.False(PinRotation.ShouldRotate(boundary, boundary));
    }

    [Fact]
    public void UNT_PIN_08_a_long_stale_pin_rotates_once_not_once_per_missed_week() =>
        // Three weeks stale. ShouldRotate returns a bool, so the only way this
        // can report "catch up three times" is if a caller loops on it.
        Assert.True(PinRotation.ShouldRotate(At(2026, 7, 25, 0, 0), At(2026, 8, 19, 10, 0)));

    [Fact]
    public void UNT_PIN_09_CRITICAL_one_millisecond_before_the_boundary_still_rotates() =>
        Assert.True(PinRotation.ShouldRotate(
            At(2026, 8, 14, 23, 59, 59, 999),
            At(2026, 8, 15, 0, 0, 0, 0)));

    [Fact]
    public void UNT_PIN_10_sunday_morning_never_rotates() =>
        // The service itself must never cross a rotation. Paired at any point on
        // Saturday, still valid all Sunday.
        Assert.False(PinRotation.ShouldRotate(At(2026, 8, 15, 18, 0), At(2026, 8, 16, 9, 30)));
}
