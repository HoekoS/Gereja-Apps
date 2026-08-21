using System.Xml;
using System.Xml.Linq;

using ChurchProjection.Application.Import;

namespace ChurchProjection.Infrastructure.Import;

/// <summary>
/// OpenLyrics is what other projection software exports, so it is the format a
/// church switching to this system arrives with.
/// </summary>
public sealed class OpenLyricsParser : IImportParser
{
    private static readonly XNamespace Ns = "http://openlyrics.info/namespace/2009/song";

    public bool CanHandle(string fileName, ReadOnlySpan<byte> head) =>
        Path.GetExtension(fileName).Equals(".xml", StringComparison.OrdinalIgnoreCase)
        && Contains(head, "openlyrics.info");

    public ImportPayload Parse(Stream input, string fileName)
    {
        XDocument document;

        try
        {
            document = XDocument.Load(input, LoadOptions.SetLineInfo);
        }
        catch (XmlException error)
        {
            throw new ImportException(
                $"'{fileName}' is not valid XML: {error.Message} (line {error.LineNumber}, position {error.LinePosition}).");
        }

        var root = document.Root
            ?? throw new ImportException($"'{fileName}' has no root element.");

        var title = root.Element(Ns + "properties")?.Element(Ns + "titles")?.Element(Ns + "title")?.Value.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ImportException($"'{fileName}' has no <title>. A song without a title cannot be searched for.");
        }

        var authors = root.Element(Ns + "properties")?.Element(Ns + "authors")?
            .Elements(Ns + "author").Select(author => author.Value.Trim()).ToArray() ?? [];

        var pages = root.Element(Ns + "lyrics")?.Elements(Ns + "verse")
            .Select((verse, index) => new ImportedPage(
                index,
                verse.Attribute("name")?.Value,
                LineText(verse.Element(Ns + "lines"))))
            .Where(page => page.Text.Length > 0)
            .ToArray() ?? [];

        if (pages.Length == 0)
        {
            throw new ImportException($"'{title}' has no <verse> elements with any lines.");
        }

        var song = new ImportedSong(
            title,
            authors.Length == 0 ? null : string.Join(", ", authors),
            root.Element(Ns + "properties")?.Element(Ns + "ccliNo")?.Value,
            root.Attribute("lang")?.Value,
            pages);

        return new ImportPayload(ImportKind.Song, [song], [], Translation: null);
    }

    /// <summary>A &lt;br/&gt; is a line break on the projected page, not whitespace.</summary>
    private static string LineText(XElement? lines) =>
        lines is null
            ? string.Empty
            : string.Concat(lines.Nodes().Select(node => node switch
            {
                XText text => text.Value,
                XElement { Name.LocalName: "br" } => "\n",
                XElement element => element.Value,
                _ => string.Empty,
            })).Trim();

    internal static bool Contains(ReadOnlySpan<byte> head, string needle) =>
        System.Text.Encoding.UTF8.GetString(head).Contains(needle, StringComparison.OrdinalIgnoreCase);
}
