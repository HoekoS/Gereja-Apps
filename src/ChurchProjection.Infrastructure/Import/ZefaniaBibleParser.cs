using System.Xml;
using System.Xml.Linq;

using ChurchProjection.Application.Import;

namespace ChurchProjection.Infrastructure.Import;

/// <summary>
/// Zefania XML is how Terjemahan Baru and Terjemahan Lama are distributed.
/// Book numbers are canonical and are stored as given — nothing here clamps
/// them to 1..66, because a deuterocanonical edition is a real file someone
/// may hand the administrator.
/// </summary>
public sealed class ZefaniaBibleParser : IImportParser
{
    public bool CanHandle(string fileName, ReadOnlySpan<byte> head) =>
        Path.GetExtension(fileName).Equals(".xml", StringComparison.OrdinalIgnoreCase)
        && OpenLyricsParser.Contains(head, "XMLBIBLE");

    public ImportPayload Parse(Stream input, string fileName)
    {
        XDocument document;

        try
        {
            // Loaded whole. A streaming reader could hand back 30,000 verses and
            // then fail on the last one, which is the half-imported Bible
            // FR-IMP-05 exists to prevent.
            document = XDocument.Load(input, LoadOptions.SetLineInfo);
        }
        catch (XmlException error)
        {
            throw new ImportException(
                $"'{fileName}' is not valid XML: {error.Message} (line {error.LineNumber}, position {error.LinePosition}).");
        }

        var root = document.Root
            ?? throw new ImportException($"'{fileName}' has no root element.");

        var verses = new List<ImportedVerse>();
        var books = new List<ImportedBookName>();

        foreach (var book in root.Elements("BIBLEBOOK"))
        {
            var bookId = RequiredInt(book, "bnumber", fileName);

            books.Add(new ImportedBookName(
                bookId,
                book.Attribute("bname")?.Value ?? $"Book {bookId}",
                book.Attribute("bsname")?.Value));

            foreach (var chapter in book.Elements("CHAPTER"))
            {
                var chapterNumber = RequiredInt(chapter, "cnumber", fileName);

                foreach (var verse in chapter.Elements("VERS"))
                {
                    verses.Add(new ImportedVerse(
                        bookId,
                        chapterNumber,
                        RequiredInt(verse, "vnumber", fileName),
                        verse.Value.Trim()));
                }
            }
        }

        if (verses.Count == 0)
        {
            throw new ImportException($"'{fileName}' contains no <VERS> elements.");
        }

        var name = root.Attribute("biblename")?.Value ?? Path.GetFileNameWithoutExtension(fileName);
        var id = Slug(Path.GetFileNameWithoutExtension(fileName));

        var translation = new ImportedTranslation(
            id,
            root.Attribute("abbrev")?.Value ?? id.ToUpperInvariant(),
            name,
            root.Attribute("lang")?.Value ?? "id",
            books);

        return new ImportPayload(ImportKind.Bible, [], verses, translation);
    }

    private static int RequiredInt(XElement element, string attribute, string fileName)
    {
        var value = element.Attribute(attribute)?.Value;

        if (!int.TryParse(value, out var number))
        {
            var line = (element as IXmlLineInfo).LineNumber;

            throw new ImportException(
                $"'{fileName}' line {line}: <{element.Name.LocalName}> has {attribute}='{value}', which is not a number.");
        }

        return number;
    }

    private static string Slug(string value) =>
        new(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
}
