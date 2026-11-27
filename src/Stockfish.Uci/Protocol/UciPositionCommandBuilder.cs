namespace Stockfish.Uci.Protocol;

internal static class UciPositionCommandBuilder
{
    internal static string Build(string position)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (string.IsNullOrWhiteSpace(position) || position.Any(char.IsControl))
        {
            throw InvalidPosition();
        }

        var tokens = position.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int index;
        switch (tokens[0])
        {
            case "startpos":
                index = 1;
                break;
            case "fen" when tokens.Length >= 7:
                index = 7;
                if (tokens.Skip(1).Take(6).Any(token => token is "moves" or "position"))
                {
                    throw InvalidPosition();
                }

                break;
            default:
                throw InvalidPosition();
        }

        if (index < tokens.Length)
        {
            if (tokens[index++] != "moves" || index == tokens.Length)
            {
                throw InvalidPosition();
            }

            for (; index < tokens.Length; index++)
            {
                try
                {
                    _ = UciMove.Parse(tokens[index]);
                }
                catch (FormatException)
                {
                    throw InvalidPosition();
                }
            }
        }

        return "position " + string.Join(' ', tokens);
    }

    private static ArgumentException InvalidPosition() =>
        new("Expected startpos or fen with six fields, optionally followed by moves and valid UCI move tokens.", "position");
}
