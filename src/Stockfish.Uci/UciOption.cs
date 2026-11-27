namespace Stockfish.Uci;

public sealed record UciOption(
    string Name,
    string Type,
    string? DefaultValue,
    string? Minimum,
    string? Maximum,
    IReadOnlyList<string> Variables);
