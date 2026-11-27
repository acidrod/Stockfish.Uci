namespace Stockfish.Uci;

public sealed record StockfishEngineInfo(
    string Name,
    string? Author,
    IReadOnlyDictionary<string, UciOption> Options);
