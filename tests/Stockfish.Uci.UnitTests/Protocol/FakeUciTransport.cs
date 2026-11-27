using System.Collections.Concurrent;
using System.Threading.Channels;
using Stockfish.Uci.Protocol;

namespace Stockfish.Uci.UnitTests.Protocol;

internal sealed class FakeUciTransport : IUciTransport
{
    private readonly Channel<string> _responses = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _writtenCommands = Channel.CreateUnbounded<string>();
    private readonly ConcurrentQueue<string> _commands = new();

    public IReadOnlyList<string> Commands => _commands.ToArray();

    public void Respond(string line)
    {
        if (!_responses.Writer.TryWrite(line))
        {
            throw new InvalidOperationException("The fake engine has closed its output.");
        }
    }

    public void CompleteOutput() => _responses.Writer.TryComplete();

    public ValueTask<string> ReadCommandAsync(CancellationToken cancellationToken) =>
        _writtenCommands.Reader.ReadAsync(cancellationToken);

    public async ValueTask WriteLineAsync(string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _commands.Enqueue(command);
        await _writtenCommands.Writer.WriteAsync(command, cancellationToken);
    }

    public IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken) =>
        _responses.Reader.ReadAllAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        _responses.Writer.TryComplete();
        _writtenCommands.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
