// src/ChurchProjection.Api/Live/OutputCounter.cs
namespace ChurchProjection.Api.Live;

/// <summary>
/// How many projector windows are connected. The control view shows this so the
/// operator knows the screen is alive before the service starts, rather than
/// finding out during the first hymn (FR-LIV-02).
/// </summary>
public sealed class OutputCounter
{
    private int _count;

    public int Current => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);

    public void Decrement() => Interlocked.Decrement(ref _count);
}
