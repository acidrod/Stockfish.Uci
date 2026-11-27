namespace Stockfish.Uci;

public sealed class StockfishEngineOptions
{
    public StockfishEngineOptions(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("A Stockfish executable path is required.", nameof(executablePath));
        }

        ExecutablePath = executablePath;
    }

    public string ExecutablePath { get; }

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
