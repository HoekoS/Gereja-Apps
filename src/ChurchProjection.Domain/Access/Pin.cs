namespace ChurchProjection.Domain.Access;

public readonly record struct Pin(string Value)
{
    public static Pin Generate() => throw new NotImplementedException();
}
