using System.Text.RegularExpressions;

namespace ChurchProjection.Domain.Bible;

/// <summary>
/// A place in the Bible, independent of translation. VerseEnd is null for a
/// whole chapter and equal to VerseStart for a single verse — the operator
/// asked for one verse, not for a range that happens to be one long.
/// </summary>
public sealed partial record BibleReference(int BookId, int Chapter, int VerseStart, int? VerseEnd)
{
    [GeneratedRegex(
        @"^(?<book>.+?)\s*(?<chapter>\d+)(?::(?<start>\d+)(?:\s*-\s*(?<end>\d+))?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    /// <summary>
    /// Parses free-form operator input. Returns null for anything it does not
    /// understand — an unparseable reference is an ordinary outcome of typing,
    /// not an exceptional condition.
    /// </summary>
    public static BibleReference? TryParse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var match = Pattern().Match(input.Trim());

        if (!match.Success || !BookNames.TryResolve(match.Groups["book"].Value, out var bookId))
        {
            return null;
        }

        var chapter = int.Parse(match.Groups["chapter"].Value);

        if (chapter < 1)
        {
            return null;
        }

        if (!match.Groups["start"].Success)
        {
            return new BibleReference(bookId, chapter, 1, null);
        }

        var start = int.Parse(match.Groups["start"].Value);
        var end = match.Groups["end"].Success ? int.Parse(match.Groups["end"].Value) : start;

        if (start < 1 || end < start)
        {
            return null;
        }

        return new BibleReference(bookId, chapter, start, end);
    }
}
