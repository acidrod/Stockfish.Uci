namespace Stockfish.Uci.Protocol;

public sealed class UciProtocolException : Exception
{
    public UciProtocolException()
    {
    }

    public UciProtocolException(string message)
        : base(message)
    {
    }

    public UciProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public UciProtocolException(string message, string? line)
        : base(line is null ? message : $"{message} Offending line: '{line}'.")
    {
        Line = line;
    }

    public string? Line { get; }
}
