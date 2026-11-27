using Stockfish.Uci.Protocol;

namespace Stockfish.Uci;

public sealed class StockfishEngine : IAsyncDisposable
{
    private readonly UciProcessTransport _transport;

    private StockfishEngine(UciProcessTransport transport, StockfishEngineInfo info)
    {
        _transport = transport;
        Info = info;
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
            return new StockfishEngine(transport, info);
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

    public ValueTask DisposeAsync() => _transport.DisposeAsync();
}
