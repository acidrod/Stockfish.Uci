namespace Stockfish.Uci;

public sealed class StockfishEngineOptions
{
    private static readonly TimeSpan MaximumTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
    private TimeSpan _startupTimeout = TimeSpan.FromSeconds(10);
    private TimeSpan _stopTimeout = TimeSpan.FromSeconds(5);

    public StockfishEngineOptions(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("A Stockfish executable path is required.", nameof(executablePath));
        }

        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException("A fully qualified Stockfish executable path is required.", nameof(executablePath));
        }

        ExecutablePath = executablePath;
    }

    public string ExecutablePath { get; }

    public TimeSpan StartupTimeout
    {
        get => _startupTimeout;
        init => _startupTimeout = ValidateTimeout(value, nameof(StartupTimeout));
    }

    public TimeSpan StopTimeout
    {
        get => _stopTimeout;
        init => _stopTimeout = ValidateTimeout(value, nameof(StopTimeout));
    }

    private static TimeSpan ValidateTimeout(TimeSpan value, string propertyName)
    {
        if (value < TimeSpan.FromMilliseconds(1) || value > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(propertyName, "Timeout must be between one millisecond and the maximum cancellation timer duration.");
        }

        return value;
    }
}
