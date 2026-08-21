using System.Security.Cryptography;

namespace ChurchProjection.Domain.Access;

/// <summary>
/// The shared six-digit PIN. Short enough to read off a card taped to the
/// booth desk, which is exactly why it rotates weekly and why pairing is rate
/// limited.
/// </summary>
public readonly record struct Pin(string Value)
{
    public static Pin Generate() =>
        new(RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"));
}
