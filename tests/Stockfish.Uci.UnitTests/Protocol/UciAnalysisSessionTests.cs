using Stockfish.Uci.Protocol;
using Xunit;

namespace Stockfish.Uci.UnitTests.Protocol;

public sealed class UciAnalysisSessionTests
{
    private static UciAnalysisSession CreateSession(
        FakeUciTransport transport,
        Func<ValueTask>? abort = null,
        bool multiPv = true,
        TimeSpan? stopTimeout = null)
    {
        var options = new Dictionary<string, UciOption>(StringComparer.OrdinalIgnoreCase);
        if (multiPv)
        {
            options.Add("MultiPV", new UciOption("MultiPV", "spin", "1", "1", "4", []));
        }

        return new UciAnalysisSession(transport, new StockfishEngineInfo("Fake engine", null, options),
            new StockfishEngineOptions(Path.Combine(Path.GetTempPath(), "stockfish"))
            {
                StartupTimeout = TimeSpan.FromSeconds(2),
                StopTimeout = stopTimeout ?? TimeSpan.FromSeconds(1),
            },
            abort ?? transport.DisposeAsync);
    }

    [Fact]
    public async Task AnalyzeAsync_ConfiguresMultiPvAndReturnsLatestVariations()
    {
        await using var transport = new FakeUciTransport();
        await using var session = CreateSession(transport);
        transport.Respond("readyok");
        transport.Respond("info depth 1 multipv 2 score cp 13 pv d2d4");
        transport.Respond("info depth 1 multipv 1 score cp 15 pv e2e4");
        transport.Respond("info depth 2 multipv 1 score cp 20 pv e2e4 e7e5");
        transport.Respond("bestmove e2e4 ponder e7e5");

        var result = await session.AnalyzeAsync("startpos", new AnalysisLimits
        {
            Depth = 2,
            MoveTime = TimeSpan.FromTicks(1),
            MultiPv = 2,
        });

        Assert.Collection(transport.Commands,
            cmd => Assert.Equal("setoption name MultiPV value 2", cmd),
            cmd => Assert.Equal("isready", cmd),
            cmd => Assert.Equal("position startpos", cmd),
            cmd => Assert.Equal("go depth 2 movetime 1", cmd));
        Assert.Equal("e2e4", result.BestMove.Value);
        Assert.Equal("e7e5", result.PonderMove?.Value);
        Assert.Collection(result.Variations,
            pv => { Assert.Equal(1, pv.Index); Assert.Equal(2, pv.Depth); },
            pv => { Assert.Equal(2, pv.Index); Assert.Equal(13, pv.Evaluation?.Value); });

        transport.Respond("readyok");
        transport.Respond("bestmove d2d4");
        await session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1 });
        Assert.Equal("setoption name MultiPV value 1", transport.Commands[4]);
    }

    [Fact]
    public async Task AnalyzeStreamingAsync_YieldsStableOrderedSnapshots()
    {
        await using var transport = new FakeUciTransport();
        await using var session = CreateSession(transport, multiPv: false);
        transport.Respond("info string ignored");
        transport.Respond("info multipv 2 score cp 2 pv d2d4");
        transport.Respond("info multipv 1 score cp 3 pv e2e4");
        transport.Respond("info multipv 1 score cp 4 pv e2e4");
        transport.Respond("bestmove e2e4");

        var updates = new List<AnalysisUpdate>();
        await foreach (var update in session.AnalyzeStreamingAsync("startpos", new AnalysisLimits { Depth = 3 }))
        {
            updates.Add(update);
        }

        Assert.Equal(3, updates.Count);
        Assert.Single(updates[0].Variations);
        Assert.Equal(2, updates[0].Variations[0].Index);
        Assert.Equal(3, updates[1].Variations[0].Evaluation?.Value);
        Assert.Equal(4, updates[2].Variations[0].Evaluation?.Value);
        Assert.Collection(transport.Commands,
            cmd => Assert.Equal("position startpos", cmd),
            cmd => Assert.Equal("go depth 3", cmd));
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsInvalidInputsWithoutSendingCommands()
    {
        await using var transport = new FakeUciTransport();
        await using var session = CreateSession(transport);
        await Assert.ThrowsAsync<ArgumentException>(
            () => session.AnalyzeAsync("startpos", new AnalysisLimits()));
        await Assert.ThrowsAsync<ArgumentException>(
            () => session.AnalyzeAsync("startpos\nquit", new AnalysisLimits { Depth = 1 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1, MultiPv = 5 }));
        Assert.Empty(transport.Commands);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsMultiPvWhenNotAdvertised()
    {
        await using var transport = new FakeUciTransport();
        await using var session = CreateSession(transport, multiPv: false);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1, MultiPv = 2 }));
        Assert.Empty(transport.Commands);
    }

    [Fact]
    public async Task AnalyzeAsync_CancellationSendsSingleStopAndAllowsReuse()
    {
        await using var transport = new FakeUciTransport();
        var aborts = 0;
        await using var session = CreateSession(transport, () =>
        {
            aborts++;
            return transport.DisposeAsync();
        }, multiPv: false);
        using var caller = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var analysis = session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 20 }, caller.Token);
        Assert.Equal("position startpos", await transport.ReadCommandAsync(deadline.Token));
        Assert.Equal("go depth 20", await transport.ReadCommandAsync(deadline.Token));

        await caller.CancelAsync();
        Assert.Equal("stop", await transport.ReadCommandAsync(deadline.Token));
        transport.Respond("info depth 1 score cp 2 pv e2e4");
        transport.Respond("bestmove e2e4");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => analysis);
        Assert.Equal(0, aborts);
        Assert.Equal(3, transport.Commands.Count);

        transport.Respond("bestmove d2d4");
        var next = await session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1 });
        Assert.Equal("d2d4", next.BestMove.Value);
    }

    [Fact]
    public async Task AnalyzeAsync_AbortsAnUnresponsiveEngineAfterStopDeadline()
    {
        await using var transport = new FakeUciTransport();
        var aborts = 0;
        await using var session = CreateSession(transport, () =>
        {
            aborts++;
            return transport.DisposeAsync();
        }, multiPv: false, stopTimeout: TimeSpan.FromMilliseconds(100));
        using var caller = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var analysis = session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 20 }, caller.Token);
        Assert.Equal("position startpos", await transport.ReadCommandAsync(deadline.Token));
        Assert.Equal("go depth 20", await transport.ReadCommandAsync(deadline.Token));
        await caller.CancelAsync();
        Assert.Equal("stop", await transport.ReadCommandAsync(deadline.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => analysis);
        Assert.Equal(1, aborts);
        Assert.Equal(3, transport.Commands.Count);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1 }));
    }

    [Fact]
    public async Task AnalyzeStreamingAsync_EarlyDisposalStopsAndDrainsSearch()
    {
        await using var transport = new FakeUciTransport();
        await using var session = CreateSession(transport, multiPv: false);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reader = session.AnalyzeStreamingAsync("startpos", new AnalysisLimits { Depth = 20 })
            .GetAsyncEnumerator();
        var next = reader.MoveNextAsync().AsTask();
        Assert.Equal("position startpos", await transport.ReadCommandAsync(deadline.Token));
        Assert.Equal("go depth 20", await transport.ReadCommandAsync(deadline.Token));
        transport.Respond("info depth 1 score cp 2 pv e2e4");
        Assert.True(await next);

        var dispose = reader.DisposeAsync().AsTask();
        Assert.Equal("stop", await transport.ReadCommandAsync(deadline.Token));
        transport.Respond("bestmove e2e4");
        await dispose;
        Assert.Equal(3, transport.Commands.Count);

        transport.Respond("bestmove d2d4");
        Assert.Equal("d2d4", (await session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1 })).BestMove.Value);
    }

    [Fact]
    public async Task AnalyzeStreamingAsync_CancelsWhileConsumerIsPaused()
    {
        await using var transport = new FakeUciTransport();
        await using var session = CreateSession(transport, multiPv: false);
        using var caller = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var reader = session.AnalyzeStreamingAsync("startpos", new AnalysisLimits { Depth = 20 }, caller.Token)
            .GetAsyncEnumerator();
        var next = reader.MoveNextAsync().AsTask();
        Assert.Equal("position startpos", await transport.ReadCommandAsync(deadline.Token));
        Assert.Equal("go depth 20", await transport.ReadCommandAsync(deadline.Token));
        transport.Respond("info depth 1 score cp 2 pv e2e4");
        Assert.True(await next);

        await caller.CancelAsync();
        Assert.Equal("stop", await transport.ReadCommandAsync(deadline.Token));
        transport.Respond("bestmove e2e4");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.MoveNextAsync().AsTask());
        Assert.Equal(3, transport.Commands.Count);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsConcurrentSearch()
    {
        await using var transport = new FakeUciTransport();
        await using var session = CreateSession(transport, multiPv: false);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var first = session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 10 });
        Assert.Equal("position startpos", await transport.ReadCommandAsync(deadline.Token));
        Assert.Equal("go depth 10", await transport.ReadCommandAsync(deadline.Token));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1 }));

        transport.Respond("bestmove e2e4");
        Assert.Equal("e2e4", (await first).BestMove.Value);
    }

    [Fact]
    public async Task AnalyzeAsync_AbortsOnMalformedResponse()
    {
        await using var transport = new FakeUciTransport();
        var aborts = 0;
        await using var session = CreateSession(transport, () =>
        {
            aborts++;
            return transport.DisposeAsync();
        }, multiPv: false);
        transport.Respond("info score cp invalid");

        var error = await Assert.ThrowsAsync<UciProtocolException>(
            () => session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1 }));

        Assert.Equal("info score cp invalid", error.Line);
        Assert.Equal(1, aborts);
    }

    [Fact]
    public async Task AnalyzeAsync_CancellationDuringSetupDrainsReadyWithoutStop()
    {
        await using var transport = new FakeUciTransport();
        var aborts = 0;
        await using var session = CreateSession(transport, () =>
        {
            aborts++;
            return transport.DisposeAsync();
        });
        using var caller = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var search = session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 10 }, caller.Token);
        Assert.Equal("setoption name MultiPV value 1", await transport.ReadCommandAsync(deadline.Token));
        Assert.Equal("isready", await transport.ReadCommandAsync(deadline.Token));

        await caller.CancelAsync();
        transport.Respond("readyok");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
        Assert.Equal(2, transport.Commands.Count);
        Assert.Equal(0, aborts);

        transport.Respond("readyok");
        transport.Respond("bestmove e2e4");
        Assert.Equal("e2e4", (await session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1 })).BestMove.Value);
    }

    [Fact]
    public async Task AnalyzeAsync_AbortsWhenSetupTimesOut()
    {
        await using var transport = new FakeUciTransport();
        var aborts = 0;
        var options = new StockfishEngineOptions(Path.Combine(Path.GetTempPath(), "stockfish"))
        {
            StartupTimeout = TimeSpan.FromMilliseconds(100),
        };
        var info = new StockfishEngineInfo("Fake engine", null,
            new Dictionary<string, UciOption> { ["MultiPV"] = new("MultiPV", "spin", "1", "1", "4", []) });
        await using var session = new UciAnalysisSession(transport, info, options, () =>
        {
            aborts++;
            return transport.DisposeAsync();
        });

        await Assert.ThrowsAsync<TimeoutException>(
            () => session.AnalyzeAsync("startpos", new AnalysisLimits { Depth = 1 }));

        Assert.Equal(1, aborts);
        Assert.Collection(transport.Commands,
            cmd => Assert.Equal("setoption name MultiPV value 1", cmd),
            cmd => Assert.Equal("isready", cmd));
    }

    [Fact]
    public async Task AnalyzeAsync_SendsOnlyMovetimeWhenDepthIsAbsent()
    {
        await using var transport = new FakeUciTransport();
        await using var session = CreateSession(transport, multiPv: false);
        transport.Respond("bestmove e2e4");

        await session.AnalyzeAsync("startpos", new AnalysisLimits { MoveTime = TimeSpan.FromMilliseconds(150) });

        Assert.Collection(transport.Commands,
            cmd => Assert.Equal("position startpos", cmd),
            cmd => Assert.Equal("go movetime 150", cmd));
    }

    [Fact]
    public async Task DisposeAsync_StopsActiveStreamEvenWhenConsumerIsPaused()
    {
        await using var transport = new FakeUciTransport();
        await using var session = CreateSession(transport, multiPv: false);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var reader = session.AnalyzeStreamingAsync("startpos", new AnalysisLimits { Depth = 40 })
            .GetAsyncEnumerator();
        var next = reader.MoveNextAsync().AsTask();
        Assert.Equal("position startpos", await transport.ReadCommandAsync(deadline.Token));
        Assert.Equal("go depth 40", await transport.ReadCommandAsync(deadline.Token));
        transport.Respond("info depth 1 score cp 2 pv e2e4");
        Assert.True(await next);

        // Fill the update buffer while the consumer is paused; cancellation must
        // also interrupt a producer that is blocked publishing a snapshot.
        for (var i = 0; i < 64; i++)
        {
            transport.Respond("info depth 2 score cp 3 pv e2e4");
        }

        var disposal = session.DisposeAsync().AsTask();
        Assert.Equal("stop", await transport.ReadCommandAsync(deadline.Token));
        transport.Respond("bestmove e2e4");
        await disposal;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.MoveNextAsync().AsTask());
        Assert.Equal(3, transport.Commands.Count);
    }
}
