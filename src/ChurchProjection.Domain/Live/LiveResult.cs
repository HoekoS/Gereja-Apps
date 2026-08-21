namespace ChurchProjection.Domain.Live;

/// <summary>
/// A refusal is a value, not an exception. The operator pressing a key that
/// cannot apply right now is normal, and normal control flow must not unwind
/// the stack in front of a congregation.
/// </summary>
public readonly record struct LiveResult(RefusalCode Refusal)
{
    public bool IsOk => Refusal == RefusalCode.None;

    public static LiveResult Ok { get; } = new(RefusalCode.None);

    public static LiveResult Refuse(RefusalCode code) => new(code);
}
