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

    [StockfishFact]
    public async Task AnalyzeAsync_ReturnsBoundedTypedAnalysisAndResetsMultiPv()
    {
        var path = Environment.GetEnvironmentVariable("STOCKFISH_UCI_PATH");
        await using var engine = await StockfishEngine.StartAsync(new StockfishEngineOptions(path!));

        var result = await engine.AnalyzeAsync("startpos moves e2e4 e7e5",
            new AnalysisLimits { Depth = 3, MultiPv = 3 });

        Assert.NotNull(result.BestMove);
        Assert.Equal(3, result.Variations.Count);
        Assert.All(result.Variations, pv =>
        {
            Assert.NotNull(pv.Evaluation);
            Assert.NotEmpty(pv.Moves);
        });
        output.WriteLine($"Best move: {result.BestMove}; variations: {result.Variations.Count}");

        var single = await engine.AnalyzeAsync("startpos moves e2e4 e7e5",
            new AnalysisLimits { Depth = 1 });
        Assert.Single(single.Variations);
    }

    [StockfishFact]
    public async Task AnalyzeStreamingAsync_StopsEarlyAndLeavesEngineReusable()
    {
        var path = Environment.GetEnvironmentVariable("STOCKFISH_UCI_PATH");
        await using var engine = await StockfishEngine.StartAsync(new StockfishEngineOptions(path!));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var receivedUpdate = false;
        await foreach (var update in engine.AnalyzeStreamingAsync("startpos",
            new AnalysisLimits { Depth = 40, MultiPv = 2 }, deadline.Token))
        {
            Assert.NotEmpty(update.Variations);
            receivedUpdate = true;
            break;
        }

        Assert.True(receivedUpdate);
        var next = await engine.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1 }, deadline.Token);
        Assert.Single(next.Variations);
    }

    [StockfishFact]
    public async Task AnalyzeAsync_NormalizesNoLegalMoveToNullMove()
    {
        var path = Environment.GetEnvironmentVariable("STOCKFISH_UCI_PATH");
        await using var engine = await StockfishEngine.StartAsync(new StockfishEngineOptions(path!));

        var result = await engine.AnalyzeAsync("fen 8/8/8/8/8/5k2/6q1/7K w - - 0 1",
            new AnalysisLimits { Depth = 1 });

        Assert.Equal("0000", result.BestMove.Value);
        Assert.Null(result.PonderMove);
    }

    [StockfishFact]
    public async Task AnalyzeStreamingAsync_CallerCancellationKeepsEngineReusable()
    {
        var path = Environment.GetEnvironmentVariable("STOCKFISH_UCI_PATH");
        await using var engine = await StockfishEngine.StartAsync(new StockfishEngineOptions(path!));
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var received = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var update in engine.AnalyzeStreamingAsync("startpos",
                new AnalysisLimits { Depth = 40 }, caller.Token))
            {
                Assert.NotEmpty(update.Variations);
                received = true;
                await caller.CancelAsync();
            }
        });

        Assert.True(received);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await engine.AnalyzeAsync("startpos",
            new AnalysisLimits { Depth = 40, MoveTime = TimeSpan.FromMilliseconds(100) }, deadline.Token);
        Assert.NotEmpty(result.Variations);
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
