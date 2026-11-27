using System.Collections.ObjectModel;

namespace Stockfish.Uci.Protocol;

internal sealed class UciSession(IUciTransport transport)
{
    public async Task<StockfishEngineInfo> InitializeAsync(
        StockfishEngineOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startup.CancelAfter(options.StartupTimeout);
        var token = startup.Token;
        var acknowledgement = "uciok";
        string? name = null;
        string? author = null;
        var engineOptions = new Dictionary<string, UciOption>(StringComparer.OrdinalIgnoreCase);

        try
        {
            token.ThrowIfCancellationRequested();
            await transport.WriteLineAsync("uci", token).ConfigureAwait(false);

            await foreach (var line in transport.ReadLinesAsync(token).WithCancellation(token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                if (acknowledgement == "readyok")
                {
                    if (line == "readyok")
                    {
                        return new StockfishEngineInfo(name!, author,
                            new ReadOnlyDictionary<string, UciOption>(engineOptions));
                    }

                    continue;
                }

                if (line == "uciok")
                {
                    if (name is null)
                    {
                        throw new UciProtocolException("Engine name was not supplied before uciok.", line);
                    }

                    acknowledgement = "readyok";
                    await transport.WriteLineAsync("isready", token).ConfigureAwait(false);
                    continue;
                }

                var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                {
                    continue;
                }

                if (tokens[0] == "id" && tokens.Length >= 2)
                {
                    switch (tokens[1])
                    {
                        case "name":
                            name = ReadIdentity(tokens, line);
                            break;
                        case "author":
                            author = ReadIdentity(tokens, line);
                            break;
                    }
                }
                else if (tokens[0] == "option")
                {
                    var option = ParseOption(tokens, line);
                    engineOptions[option.Name] = option;
                }
            }

            token.ThrowIfCancellationRequested();
            throw new UciProtocolException($"Engine output ended before receiving {acknowledgement}.");
        }
        catch (OperationCanceledException exception) when (
            startup.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Engine startup timed out waiting for {acknowledgement}.", exception);
        }
    }

    private static string ReadIdentity(string[] tokens, string line)
    {
        if (tokens.Length < 3)
        {
            throw new UciProtocolException($"Missing engine id {tokens[1]} value.", line);
        }

        return string.Join(' ', tokens, 2, tokens.Length - 2);
    }

    private static UciOption ParseOption(string[] tokens, string line)
    {
        var typeIndex = Array.IndexOf(tokens, "type", 1);

        if (tokens.Length < 5 || tokens[1] != "name" || typeIndex < 3 ||
            typeIndex == tokens.Length - 1 || IsOptionField(tokens[typeIndex + 1]))
        {
            throw new UciProtocolException("Malformed option declaration; expected name and type.", line);
        }

        var name = string.Join(' ', tokens, 2, typeIndex - 2);
        var type = tokens[typeIndex + 1];
        string? defaultValue = null;
        string? minimum = null;
        string? maximum = null;
        var variables = new List<string>();

        var index = typeIndex + 2;
        while (index < tokens.Length)
        {
            var field = tokens[index++];
            if (!IsOptionField(field))
            {
                continue;
            }

            var start = index;
            while (index < tokens.Length && !IsOptionField(tokens[index]))
            {
                index++;
            }

            if (index == start && field != "default")
            {
                throw new UciProtocolException($"Missing option {field} value.", line);
            }

            var value = string.Join(' ', tokens, start, index - start);
            switch (field)
            {
                case "default":
                    defaultValue = value;
                    break;
                case "min":
                    minimum = value;
                    break;
                case "max":
                    maximum = value;
                    break;
                case "var":
                    variables.Add(value);
                    break;
            }
        }

        return new UciOption(name, type, defaultValue, minimum, maximum, variables.AsReadOnly());
    }

    private static bool IsOptionField(string token) => token is "default" or "min" or "max" or "var";
}
