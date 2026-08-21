namespace ChurchProjection.Application.Import;

/// <summary>
/// Thrown when a file cannot be turned into a payload. Detail names the
/// offending record and is shown to the operator, so it must say which line or
/// which verse — "invalid file" tells a volunteer nothing.
/// </summary>
public sealed class ImportException(string detail) : Exception(detail)
{
    public string Detail { get; } = detail;
}
