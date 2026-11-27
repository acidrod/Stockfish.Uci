namespace Stockfish.Uci;

public sealed record PrincipalVariation(
    int Index,
    Evaluation? Evaluation,
    int? Depth,
    int? SelectiveDepth,
    long? Nodes,
    long? NodesPerSecond,
    TimeSpan? Elapsed,
    IReadOnlyList<UciMove> Moves);
