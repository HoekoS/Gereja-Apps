// Unit tests for the live aggregate (SRS FR-LIV-*, TEST-CASES UNT-LIV-*).
//
// LiveSession is the one type in the system with real branching logic and no
// I/O, which is why it gets unit tests and the repositories do not. It takes an
// IServiceOrder rather than a repository, so nothing here needs a database, a
// host, or a mocking framework.
//
// RED PHASE: ChurchProjection.Domain does not exist yet. These tests define its
// contract and will fail to compile until it does.

using ChurchProjection.Domain.Live;

namespace ChurchProjection.Domain.Tests;

public class LiveSessionTests
{
    // A service of three items. B is a 4-page song. C is a media item whose file
    // is missing from disk (the FR-LIV-17 fixture).
    private static readonly FakeServiceOrder Order = new(new()
    {
        ["A"] = (Pages: 1, MediaOk: true),
        ["B"] = (Pages: 4, MediaOk: true),
        ["C"] = (Pages: 1, MediaOk: false),
    });

    /// <summary>Runs a sequence of commands, failing the test if any is refused.</summary>
    private static LiveSession Run(params Action<LiveSession>[] commands)
    {
        var session = LiveSession.New();
        foreach (var command in commands)
        {
            command(session);
        }

        return session;
    }

    private static Action<LiveSession> Preview(string id, int page = 0) =>
        s => Assert.True(s.PreviewItem(id, page, Order).IsOk, $"preview {id} was refused");

    private static readonly Action<LiveSession> Go =
        s => Assert.True(s.Go().IsOk, "go was refused");

    private static readonly Action<LiveSession> Advance =
        s => Assert.True(s.Advance(Order).IsOk, "advance was refused");

    private static readonly Action<LiveSession> Back =
        s => Assert.True(s.Back().IsOk, "back was refused");

    private static readonly Action<LiveSession> Clear =
        s => Assert.True(s.Clear().IsOk, "clear was refused");

    private static Action<LiveSession> Blackout(bool on) =>
        s => Assert.True(s.SetBlackout(on).IsOk, "blackout was refused");

    private static Action<LiveSession> Skip(string id) =>
        s => Assert.True(s.Skip(id, Order).IsOk, $"skip {id} was refused");

    private static Action<LiveSession> Unskip(string id) =>
        s => Assert.True(s.Unskip(id).IsOk, $"unskip {id} was refused");

    // --- UNT-LIV-01 ---------------------------------------------------------

    [Fact]
    public void UNT_LIV_01_a_new_session_is_empty()
    {
        var session = LiveSession.New();

        Assert.Null(session.Live);
        Assert.Null(session.Preview);
        Assert.False(session.Blackout);
        Assert.Empty(session.Skipped);
    }

    // --- UNT-LIV-02 — CRITICAL ----------------------------------------------

    [Fact]
    public void UNT_LIV_02_previewing_does_not_touch_live()
    {
        var session = Run(Preview("A"), Go, Preview("B", 2));

        Assert.Equal(new ItemId("A"), session.Live!.ItemId);
        Assert.Equal(0, session.Live.PageIndex);
        Assert.Equal(new ItemId("B"), session.Preview!.ItemId);
        Assert.Equal(2, session.Preview.PageIndex);
    }

    // --- UNT-LIV-03 / 04 ----------------------------------------------------

    [Fact]
    public void UNT_LIV_03_go_promotes_preview_to_live_and_clears_preview()
    {
        var session = Run(Preview("B", 2), Go);

        Assert.Equal(new ItemId("B"), session.Live!.ItemId);
        Assert.Equal(2, session.Live.PageIndex); // go takes the previewed page, not page 0
        Assert.Null(session.Preview);
    }

    [Fact]
    public void UNT_LIV_04_go_with_nothing_previewed_is_refused_and_changes_nothing()
    {
        var session = Run(Preview("A"), Go);
        var before = session.Snapshot();

        var result = session.Go();

        Assert.Equal(RefusalCode.NoPreview, result.Refusal);
        AssertUnchanged(before, session);
    }

    // --- UNT-LIV-05 to 09 ---------------------------------------------------

    [Fact]
    public void UNT_LIV_05_advance_moves_forward_one_page()
    {
        var session = Run(Preview("B"), Go, Advance);

        Assert.Equal(1, session.Live!.PageIndex);
    }

    [Fact]
    public void UNT_LIV_06_back_moves_back_one_page()
    {
        var session = Run(Preview("B", 2), Go, Back);

        Assert.Equal(1, session.Live!.PageIndex);
    }

    [Fact]
    public void UNT_LIV_07_CRITICAL_advance_on_the_last_page_holds()
    {
        // B has 4 pages, so index 3 is the last.
        var session = Run(Preview("B", 3), Go);

        var result = session.Advance(Order);

        Assert.True(result.IsOk, "holding at the end is not an error");
        Assert.Equal(3, session.Live!.PageIndex);              // must not wrap to page 0
        Assert.Equal(new ItemId("B"), session.Live.ItemId);    // must not fall into the next item
    }

    [Fact]
    public void UNT_LIV_08_back_on_the_first_page_holds()
    {
        var session = Run(Preview("B", 0), Go);

        var result = session.Back();

        Assert.True(result.IsOk);
        Assert.Equal(0, session.Live!.PageIndex);
    }

    [Fact]
    public void UNT_LIV_09_advance_with_nothing_live_is_refused()
    {
        var session = LiveSession.New();
        var before = session.Snapshot();

        var result = session.Advance(Order);

        Assert.Equal(RefusalCode.NoLiveItem, result.Refusal);
        AssertUnchanged(before, session);
    }

    // --- UNT-LIV-10 / 11 — CRITICAL -----------------------------------------

    [Fact]
    public void UNT_LIV_10_CRITICAL_blackout_preserves_the_live_item_and_page()
    {
        var session = Run(Preview("B", 2), Go, Blackout(true));

        Assert.True(session.Blackout);
        Assert.Equal(new ItemId("B"), session.Live!.ItemId); // blackout hides, it does not clear
        Assert.Equal(2, session.Live.PageIndex);
    }

    [Fact]
    public void UNT_LIV_11_CRITICAL_releasing_blackout_returns_the_same_item_and_page()
    {
        var session = Run(Preview("B", 2), Go, Blackout(true), Blackout(false));

        Assert.False(session.Blackout);
        Assert.Equal(new ItemId("B"), session.Live!.ItemId);
        Assert.Equal(2, session.Live.PageIndex);
    }

    // --- UNT-LIV-12 ---------------------------------------------------------

    [Fact]
    public void UNT_LIV_12_clear_empties_live_and_leaves_preview_alone()
    {
        var session = Run(Preview("A"), Go, Preview("B"), Clear);

        Assert.Null(session.Live);
        Assert.Equal(new ItemId("B"), session.Preview!.ItemId); // clear targets live only
    }

    // --- UNT-LIV-13 — CRITICAL ----------------------------------------------

    [Fact]
    public void UNT_LIV_13_CRITICAL_go_is_refused_when_the_media_file_is_missing()
    {
        // C's file is absent from disk. It must fail in preview, never on air.
        var session = Run(Preview("A"), Go, Preview("C"));

        var result = session.Go();

        Assert.Equal(RefusalCode.MediaUnavailable, result.Refusal);
        Assert.Equal(new ItemId("A"), session.Live!.ItemId); // the congregation still sees the previous item
    }

    // --- UNT-LIV-14 ---------------------------------------------------------

    [Fact]
    public void UNT_LIV_14_previewing_an_unknown_item_is_refused()
    {
        var session = LiveSession.New();
        var before = session.Snapshot();

        var result = session.PreviewItem("NOPE", 0, Order);

        Assert.Equal(RefusalCode.UnknownItem, result.Refusal);
        AssertUnchanged(before, session);
    }

    // --- UNT-LIV-15 to 17 ---------------------------------------------------

    [Fact]
    public void UNT_LIV_15_skip_records_the_item_without_disturbing_live_or_preview()
    {
        var session = Run(Preview("A"), Go, Preview("B"), Skip("C"));

        Assert.Equal(new[] { new ItemId("C") }, session.Skipped);
        Assert.Equal(new ItemId("A"), session.Live!.ItemId);
        Assert.Equal(new ItemId("B"), session.Preview!.ItemId);
    }

    [Fact]
    public void UNT_LIV_16_unskip_removes_the_item()
    {
        var session = Run(Skip("C"), Unskip("C"));

        Assert.Empty(session.Skipped);
    }

    [Fact]
    public void UNT_LIV_17_CRITICAL_a_skipped_item_can_still_be_shown()
    {
        // Skipping is a note about this run, not a lock. URS-LIVE-09 requires the
        // operator can come back to it.
        var session = Run(Skip("B"), Preview("B"), Go);

        Assert.Equal(new ItemId("B"), session.Live!.ItemId);
    }

    // --- UNT-LIV-18 — CRITICAL ----------------------------------------------

    [Fact]
    public void UNT_LIV_18_CRITICAL_a_refused_command_leaves_the_session_untouched()
    {
        // The JavaScript design returned a new state, so "never mutates its
        // input" was the invariant. A mutable aggregate needs the same guarantee
        // stated the other way round: every guard runs to completion before the
        // first field is written.
        var session = Run(Preview("B"), Go, Advance);

        var refusals = new List<Func<LiveResult>>
        {
            () => session.PreviewItem("NOPE", 0, Order),
            () => session.Skip("NOPE", Order),
            () => session.PreviewItem("B", -1, Order),
            () => session.PreviewItem("B", 99, Order),
        };

        foreach (var refusal in refusals)
        {
            var before = session.Snapshot();

            var result = refusal();

            Assert.False(result.IsOk, "this call was expected to be refused");
            AssertUnchanged(before, session);
        }
    }

    // --- UNT-LIV-19 — CRITICAL ----------------------------------------------

    [Fact]
    public void UNT_LIV_19_CRITICAL_a_free_form_push_leaves_the_service_order_untouched()
    {
        // FR-LIV-15 / URS-LIVE-07. Live reads the service through IServiceOrder,
        // which exposes no way to write. This test asserts the interface stays
        // read-only: if a mutating member is ever added, it stops compiling.
        var members = typeof(IServiceOrder).GetMembers()
            .Select(m => m.Name)
            .Order()
            .ToArray();

        Assert.Equal(new[] { "Contains", "MediaAvailable", "PageCount" }, members);

        var session = Run(Preview("A"), Go, Preview("B"), Go);
        Assert.Equal(new ItemId("B"), session.Live!.ItemId);
    }

    // --- UNT-LIV-20 ---------------------------------------------------------

    [Fact]
    public void UNT_LIV_20_a_page_index_outside_the_item_is_refused()
    {
        // The JavaScript design dispatched on a command string, so an
        // unrecognised command was a case worth testing. C# removes that case at
        // the type level: there is no method to call. What remains worth testing
        // is the argument the type system cannot constrain.
        //
        // The unrecognised-command-string case now lives at the API boundary,
        // where JSON is deserialised. See SYS-LIV-13.
        var session = LiveSession.New();

        Assert.Equal(RefusalCode.PageOutOfRange, session.PreviewItem("B", 99, Order).Refusal);
        Assert.Equal(RefusalCode.PageOutOfRange, session.PreviewItem("B", -1, Order).Refusal);
    }

    // --- helpers ------------------------------------------------------------

    private static void AssertUnchanged(LiveSnapshot before, LiveSession session)
    {
        var after = session.Snapshot();

        Assert.Equal(before.Live, after.Live);
        Assert.Equal(before.Preview, after.Preview);
        Assert.Equal(before.Blackout, after.Blackout);
        Assert.Equal(before.Skipped, after.Skipped);
    }

    private sealed class FakeServiceOrder(Dictionary<string, (int Pages, bool MediaOk)> items)
        : IServiceOrder
    {
        public bool Contains(ItemId id) => items.ContainsKey(id.Value);

        public int PageCount(ItemId id) =>
            items.TryGetValue(id.Value, out var item) ? item.Pages : 0;

        public bool MediaAvailable(ItemId id) =>
            items.TryGetValue(id.Value, out var item) && item.MediaOk;
    }
}
