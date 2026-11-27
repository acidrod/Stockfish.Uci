namespace Stockfish.Uci;

public sealed record AnalysisResult(
    UciMove BestMove,
    UciMove? PonderMove,
    IReadOnlyList<PrincipalVariation> Variations,
    TimeSpan Elapsed);
