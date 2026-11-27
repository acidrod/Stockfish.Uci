using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Stockfish.Uci.IntegrationTests;

public sealed class Stockfish18IntegrationTests(ITestOutputHelper output)
{
    [StockfishFact]
    public async Task StartAsync_DiscoversEngineAndDisposesOwnedProcess()
    {
        var path = Environment.GetEnvironmentVariable("STOCKFISH_UCI_PATH");
        Assert.False(string.IsNullOrWhiteSpace(path));

        await using var engine = await StockfishEngine.StartAsync(new StockfishEngineOptions(path!));
        using var process = Process.GetProcessById(engine.ProcessId);

        output.WriteLine($"Connected to {engine.Info.Name}");
        Assert.Contains("Stockfish", engine.Info.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Threads", engine.Info.Options.Keys);
        Assert.False(process.HasExited);

        await engine.DisposeAsync();
        Assert.True(process.HasExited);
        await engine.DisposeAsync();
    }
}

internal sealed class StockfishFactAttribute : FactAttribute
{
    public StockfishFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKFISH_UCI_PATH")))
        {
            Skip = "Set STOCKFISH_UCI_PATH to an installed Stockfish executable to run integration tests.";
        }
    }
}
