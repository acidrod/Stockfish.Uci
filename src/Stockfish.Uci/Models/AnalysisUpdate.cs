namespace Stockfish.Uci;

public sealed record AnalysisUpdate(IReadOnlyList<PrincipalVariation> Variations);
