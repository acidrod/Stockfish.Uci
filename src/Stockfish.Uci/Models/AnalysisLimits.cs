namespace Stockfish.Uci;

public sealed record AnalysisLimits
{
    public int? Depth { get; init; }

    public TimeSpan? MoveTime { get; init; }

    public int MultiPv { get; init; } = 1;

    public void Validate()
    {

        if (Depth is null && MoveTime is null)
        {
            throw new ArgumentException("At least one analysis bound must be configured.");
        }

        if (Depth is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Depth), "Depth must be positive.");
        }

        if (MoveTime is { } moveTime && moveTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MoveTime), "Move time must be positive.");
        }

        if (MultiPv < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MultiPv), "MultiPV must be at least one.");
        }
    }
}
