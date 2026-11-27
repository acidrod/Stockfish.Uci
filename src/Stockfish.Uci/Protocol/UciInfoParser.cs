using System.Globalization;

namespace Stockfish.Uci.Protocol;

internal static class UciInfoParser
{
    internal static bool IsBestMove(string line)
    {
        var tokens = Tokenize(line);
        return tokens.Length != 0 && tokens[0] == "bestmove";
    }

    internal static PrincipalVariation? ParseInfo(string line)
    {
        var tokens = Tokenize(line);
        if (tokens.Length == 0 || tokens[0] != "info")
        {
            return null;
        }

        var index = 1;
        int? depth = null;
        int? selectiveDepth = null;
        long? nodes = null;
        long? nodesPerSecond = null;
        TimeSpan? elapsed = null;
        Evaluation? evaluation = null;
        var moves = new List<UciMove>();

        var i = 1;
        while (i < tokens.Length)
        {
            var field = tokens[i++];
            switch (field)
            {
                case "string":
                    i = tokens.Length;
                    break;
                case "depth":
                    depth = ReadInt(tokens, ref i, field, 0, line);
                    break;
                case "seldepth":
                    selectiveDepth = ReadInt(tokens, ref i, field, 0, line);
                    break;
                case "multipv":
                    index = ReadInt(tokens, ref i, field, 1, line);
                    break;
                case "nodes":
                    nodes = ReadLong(tokens, ref i, field, line);
                    break;
                case "nps":
                    nodesPerSecond = ReadLong(tokens, ref i, field, line);
                    break;
                case "time":
                    var milliseconds = ReadLong(tokens, ref i, field, line);
                    if (milliseconds > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond)
                    {
                        throw Malformed(field, line);
                    }

                    elapsed = TimeSpan.FromTicks(milliseconds * TimeSpan.TicksPerMillisecond);
                    break;
                case "score":
                    var kind = ReadToken(tokens, ref i, field, line) switch
                    {
                        "cp" => EvaluationKind.Centipawns,
                        "mate" => EvaluationKind.Mate,
                        _ => throw Malformed(field, line),
                    };
                    evaluation = new Evaluation(kind, ReadInt(tokens, ref i, field, int.MinValue, line));
                    break;
                case "pv":
                    var start = i;
                    while (i < tokens.Length && !IsInfoField(tokens[i]))
                    {
                        moves.Add(ParseMove(tokens[i++], line));
                    }

                    if (i == start)
                    {
                        throw Malformed(field, line);
                    }

                    break;
            }
        }

        return evaluation is null && moves.Count == 0
            ? null
            : new PrincipalVariation(index, evaluation, depth, selectiveDepth, nodes, nodesPerSecond,
                elapsed, moves.AsReadOnly());
    }

    internal static (UciMove BestMove, UciMove? PonderMove) ParseBestMove(string line)
    {
        var tokens = Tokenize(line);
        if (tokens.Length is not (2 or 4) || tokens[0] != "bestmove" ||
            (tokens.Length == 4 && tokens[2] != "ponder"))
        {
            throw Malformed("bestmove", line);
        }

        // Stockfish uses (none) when no legal move exists; normalize to the UCI null move.
        var best = ParseMove(tokens[1] == "(none)" ? "0000" : tokens[1], line);
        var ponder = tokens.Length == 4 ? ParseMove(tokens[3], line) : null;
        return (best, ponder);
    }

    private static string[] Tokenize(string line) =>
        line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static bool IsInfoField(string token) => token is
        "depth" or "seldepth" or "multipv" or "score" or "nodes" or "nps" or "time" or "pv" or
        "string" or "currmove" or "currmovenumber" or "hashfull" or "tbhits" or "sbhits" or
        "cpuload" or "wdl" or "refutation" or "currline" or "lowerbound" or "upperbound";

    private static string ReadToken(string[] tokens, ref int index, string field, string line) =>
        index < tokens.Length ? tokens[index++] : throw Malformed(field, line);

    private static int ReadInt(string[] tokens, ref int index, string field, int minimum, string line)
    {
        var token = ReadToken(tokens, ref index, field, line);
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum
            ? value
            : throw Malformed(field, line);
    }

    private static long ReadLong(string[] tokens, ref int index, string field, string line)
    {
        var token = ReadToken(tokens, ref index, field, line);
        return long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : throw Malformed(field, line);
    }

    private static UciMove ParseMove(string token, string line)
    {
        try
        {
            return UciMove.Parse(token);
        }
        catch (FormatException)
        {
            throw Malformed("move", line);
        }
    }

    private static UciProtocolException Malformed(string field, string line) =>
        new($"Malformed UCI {field} value.", line);
}
