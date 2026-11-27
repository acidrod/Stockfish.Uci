using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Stockfish.Uci.Protocol;

internal sealed class UciProcessTransport : IUciTransport
{
    private const int DiagnosticCapacity = 8192;
    private readonly Process _process;
    private readonly StreamWriter _input;
    private readonly StreamReader _output;
    private readonly StreamReader _error;
    private readonly TimeSpan _stopTimeout;
    private readonly CancellationTokenSource _errorCancellation = new();
    private readonly StringBuilder _diagnostics = new(DiagnosticCapacity);
    private readonly object _diagnosticLock = new();
    private readonly object _disposeLock = new();
    private readonly Task _errorDrain;
    private Task? _disposeTask;

    private UciProcessTransport(Process process, TimeSpan stopTimeout)
    {
        _process = process;
        _input = process.StandardInput;
        _output = process.StandardOutput;
        _error = process.StandardError;
        _stopTimeout = stopTimeout;
        ProcessId = process.Id;
        _errorDrain = DrainStandardErrorAsync();
    }

    internal int ProcessId { get; }

    internal string StandardErrorTail
    {
        get
        {
            lock (_diagnosticLock)
            {
                return _diagnostics.ToString();
            }
        }
    }

    internal static UciProcessTransport Start(StockfishEngineOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(options.ExecutablePath),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = startInfo };
        var started = false;
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start engine '{options.ExecutablePath}'.");
            }

            started = true;
            return new UciProcessTransport(process, options.StopTimeout);
        }
        catch (Exception exception)
        {
            try
            {
                // If initialization failed after launching the process, do not leave it running.
                if (started && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException) when (started && process.HasExited)
            {
            }
            finally
            {
                process.Dispose();
            }

            if (exception is Win32Exception win32Exception)
            {
                var osError = new Win32Exception(win32Exception.NativeErrorCode);
                throw new InvalidOperationException(
                    $"Could not start engine '{options.ExecutablePath}': OS error {osError.NativeErrorCode} ({osError.Message}).",
                    osError);
            }

            throw;
        }
    }

    public async ValueTask WriteLineAsync(string command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Any(char.IsControl))
        {
            throw new ArgumentException("UCI commands must not contain control characters.", nameof(command));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _input.WriteLineAsync(command.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> ReadLinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _output.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            yield return line;
        }
    }

    public ValueTask DisposeAsync() => BeginDispose(force: false);

    internal ValueTask AbortAsync() => BeginDispose(force: true);

    private ValueTask BeginDispose(bool force)
    {
        lock (_disposeLock)
        {
            return new ValueTask(_disposeTask ??= DisposeCoreAsync(force));
        }
    }

    private async Task DrainStandardErrorAsync()
    {
        var chunk = new char[1024];
        try
        {
            int count;
            while ((count = await _error.ReadAsync(chunk.AsMemory(), _errorCancellation.Token)
                .ConfigureAwait(false)) != 0)
            {
                lock (_diagnosticLock)
                {
                    _diagnostics.Append(chunk, 0, count);
                    if (_diagnostics.Length > DiagnosticCapacity)
                    {
                        _diagnostics.Remove(0, _diagnostics.Length - DiagnosticCapacity);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_errorCancellation.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
            // The child may close its diagnostic stream while exiting.
        }
        catch (ObjectDisposedException) when (_errorCancellation.IsCancellationRequested)
        {
        }
    }

    private async Task DisposeCoreAsync(bool force)
    {
        try
        {
            if (force)
            {
                await KillAndWaitAsync().ConfigureAwait(false);
            }
            else if (!_process.HasExited)
            {
                using var gracefulTimeout = new CancellationTokenSource(_stopTimeout);
                try
                {
                    await WriteLineAsync("quit", gracefulTimeout.Token).ConfigureAwait(false);
                }
                catch (IOException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (OperationCanceledException) when (gracefulTimeout.IsCancellationRequested)
                {
                }

                try
                {
                    await _process.WaitForExitAsync(gracefulTimeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (gracefulTimeout.IsCancellationRequested)
                {
                    await KillAndWaitAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            try
            {
                // Normally stderr reaches EOF when the process exits. A descendant
                // might still hold its pipe, so never wait without a deadline.
                try
                {
                    await _errorDrain.WaitAsync(_stopTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    // Cancel the drain and close the pipe in the cleanup below.
                }
            }
            finally
            {
                try
                {
                    await _errorCancellation.CancelAsync().ConfigureAwait(false);
                    _output.Dispose();
                    _error.Dispose();
                    try
                    {
                        _input.Dispose();
                    }
                    catch (IOException)
                    {
                        // The process has exited and its input pipe is closed.
                    }
                }
                finally
                {
                    _process.Dispose();
                    _errorCancellation.Dispose();
                }
            }
        }
    }

    private async Task KillAndWaitAsync()
    {
        if (!_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (_process.HasExited)
            {
            }
        }

        using var killTimeout = new CancellationTokenSource(_stopTimeout);
        try
        {
            await _process.WaitForExitAsync(killTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (killTimeout.IsCancellationRequested)
        {
            throw new TimeoutException("The engine did not exit after being terminated.", exception);
        }
    }
}
