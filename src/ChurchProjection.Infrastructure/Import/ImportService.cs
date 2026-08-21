using ChurchProjection.Application.Import;

namespace ChurchProjection.Infrastructure.Import;

/// <summary>
/// Picks a parser by extension and by what the first bytes actually contain,
/// because an OpenLyrics song and a Zefania Bible are both called ".xml".
/// </summary>
public sealed class ImportService(IReadOnlyList<IImportParser> parsers) : IImportReader
{
    private const int HeadBytes = 512;

    public static ImportService WithDefaultParsers() =>
        new([new ZefaniaBibleParser(), new OpenLyricsParser(), new PlainTextSongParser()]);

    public ImportPayload Parse(Stream input, string fileName)
    {
        // ponytail: the whole file is buffered so the head can be sniffed and
        // the parser can still read from the start. Imports are a few megabytes;
        // revisit if a full-canon Bible with audio ever arrives in one upload.
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);

        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            throw new ImportException($"'{fileName}' is empty.");
        }

        var head = bytes.AsSpan(0, Math.Min(HeadBytes, bytes.Length));

        // A foreach rather than FirstOrDefault: head is a span, and a span
        // cannot be captured by a lambda.
        IImportParser? parser = null;

        foreach (var candidate in parsers)
        {
            if (candidate.CanHandle(fileName, head))
            {
                parser = candidate;
                break;
            }
        }

        if (parser is null)
        {
            throw new ImportException(
                $"Nothing here can read '{fileName}'. Supported: plain-text lyrics (.txt), OpenLyrics (.xml), Zefania Bibles (.xml).");
        }

        using var replay = new MemoryStream(bytes, writable: false);

        return parser.Parse(replay, fileName);
    }
}
