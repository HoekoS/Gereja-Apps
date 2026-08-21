namespace ChurchProjection.Application.Import;

/// <summary>Picks a parser and runs it. Implemented in Infrastructure, where
/// the parsers live.</summary>
public interface IImportReader
{
    ImportPayload Parse(Stream input, string fileName);
}
