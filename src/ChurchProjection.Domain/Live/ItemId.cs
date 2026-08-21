namespace ChurchProjection.Domain.Live;

public readonly record struct ItemId(string Value)
{
    public static implicit operator ItemId(string value) => new(value);

    public override string ToString() => Value;
}
