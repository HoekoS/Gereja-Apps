namespace ChurchProjection.Domain.Library;

public readonly record struct TranslationId(string Value)
{
    public static implicit operator TranslationId(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct SongId(string Value)
{
    public static implicit operator SongId(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct MediaId(string Value)
{
    public static implicit operator MediaId(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct ServiceId(string Value)
{
    public static implicit operator ServiceId(string value) => new(value);

    public override string ToString() => Value;
}
