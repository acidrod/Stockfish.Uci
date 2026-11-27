using Stockfish.Uci.Protocol;

namespace Stockfish.Uci;

public sealed class StockfishEngine : IAsyncDisposable
{
    private readonly UciProcessTransport _transport;
    private readonly UciAnalysisSession _analysis;

    private StockfishEngine(UciProcessTransport transport, StockfishEngineInfo info, StockfishEngineOptions options)
    {
        _transport = transport;
        Info = info;
        _analysis = new UciAnalysisSession(transport, info, options, transport.AbortAsync);
    }

    public StockfishEngineInfo Info { get; }

    /// <summary>Gets the most recent engine standard error output, limited to 8192 characters.</summary>
    public string StandardErrorTail => _transport.StandardErrorTail;

    internal int ProcessId => _transport.ProcessId;

    public static async Task<StockfishEngine> StartAsync(
        StockfishEngineOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(options.ExecutablePath))
        {
            throw new FileNotFoundException("The configured engine executable was not found.", options.ExecutablePath);
        }

        var transport = UciProcessTransport.Start(options);
        try
        {
            var info = await new UciSession(transport).InitializeAsync(options, cancellationToken).ConfigureAwait(false);
            return new StockfishEngine(transport, info, options);
        }
        catch (Exception exception)
        {
            try
            {
                await transport.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException("Engine startup and process cleanup failed.", exception, cleanupException);
            }

            var diagnostics = transport.StandardErrorTail;
            if (diagnostics.Length != 0)
            {
                exception.Data[nameof(StandardErrorTail)] = diagnostics;
            }

            throw;
        }
    }

    /// <summary>Analyzes a position and returns the final best move and principal variations.</summary>
    /// <remarks>Pass startpos or fen arguments without the position prefix. Only one search may be active per engine.</remarks>
    public Task<AnalysisResult> AnalyzeAsync(
        string position, AnalysisLimits limits, CancellationToken cancellationToken = default) =>
        _analysis.AnalyzeAsync(position, limits, cancellationToken);

    /// <summary>Streams snapshots of the latest principal variations, until bestmove or cancellation.</summary>
    /// <remarks>Disposing the enumerator early stops the active search and waits for bestmove before reusing the engine.</remarks>
    public IAsyncEnumerable<AnalysisUpdate> AnalyzeStreamingAsync(
        string position, AnalysisLimits limits, CancellationToken cancellationToken = default) =>
        _analysis.AnalyzeStreamingAsync(position, limits, cancellationToken);

    public ValueTask DisposeAsync() => _analysis.DisposeAsync();
}
