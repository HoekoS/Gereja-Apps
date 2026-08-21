namespace ChurchProjection.Application.Import;

public interface IImportParser
{
    bool CanHandle(string fileName, ReadOnlySpan<byte> head);

    ImportPayload Parse(Stream input, string fileName);
}
