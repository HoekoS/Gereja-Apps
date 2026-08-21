using System.Text;
using System.Text.RegularExpressions;

using ChurchProjection.Application.Import;

namespace ChurchProjection.Infrastructure.Import;

/// <summary>
/// The format lyrics actually arrive in: a title, a blank line, then one block
/// of lines per projected page. A block may open with a section label, either
/// bracketed or colon-terminated, because both are what people type.
/// </summary>
public sealed partial class PlainTextSongParser : IImportParser
{
    [GeneratedRegex(@"^\[(?<label>.+)\]$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedLabel();

    // ponytail: a label is a short line ending in a colon. A lyric line that
    // ends in a colon would be misread as a label; nobody writes those, and the
    // fix if they ever do is to require the block to have another line.
    [GeneratedRegex(@"^(?<label>[^:]{1,30}):$", RegexOptions.CultureInvariant)]
    private static partial Regex ColonLabel();

    public bool CanHandle(string fileName, ReadOnlySpan<byte> head) =>
        Path.GetExtension(fileName).Equals(".txt", StringComparison.OrdinalIgnoreCase);

    public ImportPayload Parse(Stream input, string fileName)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        // Word on Windows produces CRLF. Normalise once, here, so no rule below
        // has to think about it (UNT-IMP-13).
        var lines = reader.ReadToEnd().ReplaceLineEndings("\n").Split('\n');

        var titleIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));

        if (titleIndex < 0)
        {
            throw new ImportException($"'{fileName}' is empty. A song file needs a title line and at least one verse.");
        }

        var title = lines[titleIndex].Trim();
        var pages = ReadPages(lines.Skip(titleIndex + 1));

        if (pages.Count == 0)
        {
            throw new ImportException($"'{title}' has a title but no verses. Separate each projected page with a blank line.");
        }

        var song = new ImportedSong(title, Author: null, Ccli: null, Language: null, pages);

        return new ImportPayload(ImportKind.Song, [song], [], Translation: null);
    }

    private static List<ImportedPage> ReadPages(IEnumerable<string> lines)
    {
        var pages = new List<ImportedPage>();
        var block = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                AddPage(pages, block);
                block.Clear();
            }
            else
            {
                block.Add(line.TrimEnd());
            }
        }

        AddPage(pages, block);

        return pages;
    }

    private static void AddPage(List<ImportedPage> pages, List<string> block)
    {
        if (block.Count == 0)
        {
            return;
        }

        var label = ReadLabel(block[0]);
        var body = string.Join("\n", label is null ? block : block.Skip(1)).Trim();

        if (body.Length == 0)
        {
            return;
        }

        pages.Add(new ImportedPage(pages.Count, label, body));
    }

    private static string? ReadLabel(string line)
    {
        var trimmed = line.Trim();
        var bracketed = BracketedLabel().Match(trimmed);

        if (bracketed.Success)
        {
            return bracketed.Groups["label"].Value.Trim();
        }

        var colon = ColonLabel().Match(trimmed);

        return colon.Success ? colon.Groups["label"].Value.Trim() : null;
    }
}
