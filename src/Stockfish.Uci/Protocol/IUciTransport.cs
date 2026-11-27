namespace Stockfish.Uci.Protocol;

internal interface IUciTransport : IAsyncDisposable
{
    ValueTask WriteLineAsync(string command, CancellationToken cancellationToken);

    // A single active reader consumes lines in order. A later reader resumes at the next unread line.
    // Both writing and reading must honor cancellation, including while waiting on engine I/O.
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken);
}
