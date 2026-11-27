using System.Text.RegularExpressions;

namespace Stockfish.Uci;

public sealed partial class UciMove
{
    private const string NullMove = "0000";

    private UciMove(string value) => Value = value;

    public string Value { get; }

    public static UciMove Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value == NullMove || UciMovePattern().IsMatch(value))
        {
            return new UciMove(value);
        }

        throw new FormatException($"'{value}' is not a valid UCI move.");
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"\A[a-h][1-8][a-h][1-8][qrbn]?\z")]
    private static partial Regex UciMovePattern();
}
