namespace ChurchProjection.Domain.Services;

public sealed class ServiceItem
{
    public required string Id { get; init; }

    public required string Kind { get; set; }           // bible | song | slide | media | countdown

    public required string Label { get; set; }

    public required ItemRef Ref { get; set; }

    public int Position { get; set; }

    public void Update(string label, ItemRef reference)
    {
        Label = label;
        Ref = reference;
    }
}
