using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Stockfish.Uci.Protocol;

internal sealed class UciAnalysisSession(
    IUciTransport transport,
    StockfishEngineInfo info,
    StockfishEngineOptions options,
    Func<ValueTask> abort) : IAsyncDisposable
{
    private readonly object _gate = new();
    private AnalysisRun? _active;
    private Task? _disposeTask;
    private bool _disposing;
    private bool _unusable;

    internal async Task<AnalysisResult> AnalyzeAsync(
        string position, AnalysisLimits limits, CancellationToken cancellationToken = default)
    {
        var run = StartRun(position, limits, cancellationToken);
        try
        {
            await foreach (var _ in run.Updates.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
            {
                // The same pipeline supplies streaming updates and the final aggregate.
            }

            return await run.Completion.ConfigureAwait(false);
        }
        finally
        {
            await FinishConsumerAsync(run).ConfigureAwait(false);
        }
    }

    internal async IAsyncEnumerable<AnalysisUpdate> AnalyzeStreamingAsync(
        string position, AnalysisLimits limits,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var run = StartRun(position, limits, cancellationToken);
        try
        {
            await foreach (var update in run.Updates.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
            {
                run.Token.ThrowIfCancellationRequested();
                yield return update;
            }

            await run.Completion.ConfigureAwait(false);
        }
        finally
        {
            // Also stops a search when the enumerator is disposed early (e.g. await foreach break).
            await FinishConsumerAsync(run).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposing = true;
            return new ValueTask(_disposeTask ??= DisposeCoreAsync(_active));
        }
    }

    private AnalysisRun StartRun(string position, AnalysisLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        var positionCommand = UciPositionCommandBuilder.Build(position);
        var multiPvCommand = BuildMultiPv(limits);
        var goCommand = BuildGo(limits);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposing || _unusable, this);
            if (_active is not null)
            {
                throw new InvalidOperationException("An analysis is already running on this engine.");
            }

            var run = new AnalysisRun(cancellationToken);
            _active = run;
            run.Completion = ExecuteAsync(run, positionCommand, multiPvCommand, goCommand);
            return run;
        }
    }

    private async Task<AnalysisResult> ExecuteAsync(
        AnalysisRun run, string positionCommand, string? multiPvCommand, string goCommand)
    {
        try
        {
            await using var reader = new ReadCursor(transport);
            var searchStarted = false;
            var waitingForReady = false;
            var synchronized = true;
            try
            {
                run.Token.ThrowIfCancellationRequested();
                // Command writes use a separate deadline so cancellation cannot leave a partial
                // UCI command in the pipe. Check caller cancellation between complete stages.
                using (var deadline = new CancellationTokenSource(options.StartupTimeout))
                {
                    try
                    {
                        if (multiPvCommand is not null)
                        {
                            synchronized = false;
                            await transport.WriteLineAsync(multiPvCommand, deadline.Token).ConfigureAwait(false);
                            await transport.WriteLineAsync("isready", deadline.Token).ConfigureAwait(false);
                            waitingForReady = true;
                            using var readyCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                                deadline.Token, run.Token);
                            await WaitForReadyAsync(reader, readyCancellation.Token).ConfigureAwait(false);
                            waitingForReady = false;
                            synchronized = true;
                        }

                        run.Token.ThrowIfCancellationRequested();
                        synchronized = false;
                        await transport.WriteLineAsync(positionCommand, deadline.Token).ConfigureAwait(false);
                        synchronized = true;

                        run.Token.ThrowIfCancellationRequested();
                        synchronized = false;
                        await transport.WriteLineAsync(goCommand, deadline.Token).ConfigureAwait(false);
                        searchStarted = true;
                    }
                    catch (OperationCanceledException exception) when (
                        deadline.IsCancellationRequested && !run.Token.IsCancellationRequested)
                    {
                        throw new TimeoutException("Engine did not complete analysis setup before the timeout.", exception);
                    }
                }

                var clock = Stopwatch.StartNew();
                var variations = new SortedDictionary<int, PrincipalVariation>();
                while (true)
                {
                    var line = await reader.ReadAsync(run.Token).ConfigureAwait(false);
                    if (line is null)
                    {
                        throw new UciProtocolException("Engine output ended before bestmove.");
                    }

                    if (UciInfoParser.IsBestMove(line))
                    {
                        var (best, ponder) = UciInfoParser.ParseBestMove(line);
                        return new AnalysisResult(best, ponder, Snapshot(variations), clock.Elapsed);
                    }

                    var variation = UciInfoParser.ParseInfo(line);
                    if (variation is not null)
                    {
                        variations[variation.Index] = variation;
                        await run.Updates.Writer.WriteAsync(new AnalysisUpdate(Snapshot(variations)), run.Token)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException exception) when (run.Token.IsCancellationRequested)
            {
                if (searchStarted)
                {
                    if (!await StopAndDrainAsync(reader).ConfigureAwait(false))
                    {
                        await AbortAfterFailureAsync(exception).ConfigureAwait(false);
                    }
                }
                else if (waitingForReady)
                {
                    if (!await RecoverReadyAsync(reader).ConfigureAwait(false))
                    {
                        await AbortAfterFailureAsync(exception).ConfigureAwait(false);
                    }
                }
                else if (!synchronized)
                {
                    await AbortAfterFailureAsync(exception).ConfigureAwait(false);
                }

                throw;
            }
            catch (Exception exception)
            {
                // I/O and malformed protocol responses may leave the stream out of sync.
                await AbortAfterFailureAsync(exception).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            lock (_gate)
            {
                _active = null;
                run.Cancellation.Dispose();
            }

            run.Updates.Writer.TryComplete();
        }
    }

    private static async Task WaitForReadyAsync(ReadCursor reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line == "readyok")
            {
                return;
            }
        }

        throw new UciProtocolException("Engine output ended before readyok.");
    }

    private async Task<bool> RecoverReadyAsync(ReadCursor reader)
    {
        using var deadline = new CancellationTokenSource(options.StopTimeout);
        try
        {
            await WaitForReadyAsync(reader, deadline.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (UciProtocolException)
        {
        }

        return false;
    }

    private async Task<bool> StopAndDrainAsync(ReadCursor reader)
    {
        using var deadline = new CancellationTokenSource(options.StopTimeout);
        try
        {
            await transport.WriteLineAsync("stop", deadline.Token).ConfigureAwait(false);
            while (await reader.ReadAsync(deadline.Token).ConfigureAwait(false) is { } line)
            {
                if (UciInfoParser.IsBestMove(line))
                {
                    _ = UciInfoParser.ParseBestMove(line);
                    return true;
                }
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (UciProtocolException)
        {
        }

        return false;
    }

    private async Task AbortAfterFailureAsync(Exception failure)
    {
        lock (_gate)
        {
            _unusable = true;
        }

        try
        {
            await abort().ConfigureAwait(false);
        }
        catch (Exception cleanupException)
        {
            throw new AggregateException("Engine analysis and process cleanup failed.", failure, cleanupException);
        }
    }

    private void CancelRun(AnalysisRun run)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_active, run))
            {
                run.Cancellation.Cancel();
            }
        }
    }

    private async Task FinishConsumerAsync(AnalysisRun run)
    {
        CancelRun(run);
        try
        {
            await run.Completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (run.Token.IsCancellationRequested)
        {
            // A consumer intentionally ending enumeration must not receive a second cancellation.
        }
    }

    private async Task DisposeCoreAsync(AnalysisRun? run)
    {
        try
        {
            if (run is not null)
            {
                await FinishConsumerAsync(run).ConfigureAwait(false);
            }
        }
        finally
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    private string? BuildMultiPv(AnalysisLimits limits)
    {
        if (!info.Options.TryGetValue("MultiPV", out var option))
        {
            if (limits.MultiPv != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(limits), "The engine does not advertise MultiPV support.");
            }

            return null;
        }

        if (!string.Equals(option.Type, "spin", StringComparison.OrdinalIgnoreCase))
        {
            throw new UciProtocolException("The advertised MultiPV option is not a spin option.");
        }

        var min = ReadOptionBound(option.Minimum);
        var max = ReadOptionBound(option.Maximum);
        if (min is not null && max is not null && min > max)
        {
            throw new UciProtocolException("The engine advertised an invalid MultiPV range.");
        }

        if (limits.MultiPv < min || limits.MultiPv > max)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "MultiPV is outside the engine's advertised range.");
        }

        return FormattableString.Invariant($"setoption name MultiPV value {limits.MultiPv}");
    }

    private static long? ReadOptionBound(string? bound)
    {
        if (bound is null)
        {
            return null;
        }

        return long.TryParse(bound, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new UciProtocolException("The engine advertised a malformed MultiPV bound.");
    }

    private static string BuildGo(AnalysisLimits limits)
    {
        var command = "go";
        if (limits.Depth is { } depth)
        {
            command += FormattableString.Invariant($" depth {depth}");
        }

        if (limits.MoveTime is { } time)
        {
            var milliseconds = time.Ticks / TimeSpan.TicksPerMillisecond;
            if (time.Ticks % TimeSpan.TicksPerMillisecond != 0)
            {
                milliseconds++;
            }

            command += FormattableString.Invariant($" movetime {milliseconds}");
        }

        return command;
    }

    private static ReadOnlyCollection<PrincipalVariation> Snapshot(SortedDictionary<int, PrincipalVariation> variations) =>
        Array.AsReadOnly(variations.Values.ToArray());

    private sealed class AnalysisRun
    {
        internal AnalysisRun(CancellationToken cancellationToken)
        {
            Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Token = Cancellation.Token;
        }

        internal CancellationTokenSource Cancellation { get; }
        internal CancellationToken Token { get; }
        internal Channel<AnalysisUpdate> Updates { get; } = Channel.CreateBounded<AnalysisUpdate>(
            new BoundedChannelOptions(32) { SingleReader = true, SingleWriter = true });
        internal Task<AnalysisResult> Completion { get; set; } = null!;
    }

    // Keep the pending physical read alive during caller cancellation: stop must drain
    // the same stream, including a bestmove that may have arrived concurrently.
    private sealed class ReadCursor : IAsyncDisposable
    {
        private readonly CancellationTokenSource _lifetime = new();
        private readonly IAsyncEnumerator<string> _reader;
        private Task<bool>? _pending;

        internal ReadCursor(IUciTransport transport)
        {
            _reader = transport.ReadLinesAsync(_lifetime.Token).GetAsyncEnumerator(_lifetime.Token);
        }

        internal async Task<string?> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pending ??= _reader.MoveNextAsync().AsTask();
            var hasLine = await _pending.WaitAsync(cancellationToken).ConfigureAwait(false);
            _pending = null;
            return hasLine ? _reader.Current : null;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _lifetime.CancelAsync().ConfigureAwait(false);
                if (_pending is not null)
                {
                    try
                    {
                        await _pending.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
            finally
            {
                try
                {
                    await _reader.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    _lifetime.Dispose();
                }
            }
        }
    }
}
