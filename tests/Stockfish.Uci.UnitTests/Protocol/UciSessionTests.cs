using Stockfish.Uci.Protocol;
using Xunit;

namespace Stockfish.Uci.UnitTests.Protocol;

public sealed class UciSessionTests
{
    private static StockfishEngineOptions Options(TimeSpan? startupTimeout = null) =>
        new(Path.Combine(Path.GetTempPath(), "stockfish"))
        {
            StartupTimeout = startupTimeout ?? TimeSpan.FromSeconds(5),
        };

    [Fact]
    public async Task InitializeAsync_WaitsForBothAcknowledgementsAndDiscoversOptions()
    {
        await using var transport = new FakeUciTransport();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var session = new UciSession(transport);
        var initialize = session.InitializeAsync(Options(), deadline.Token);

        Assert.Equal("uci", await transport.ReadCommandAsync(deadline.Token));
        transport.Respond("Stockfish banner");
        transport.Respond("id name Stockfish 18");
        transport.Respond("id author the Stockfish developers");
        transport.Respond("option name Threads type spin default 1 min 1 max 1024");
        transport.Respond("option name UCI Chess960 type check default false");
        transport.Respond("option name Evaluation style type combo default Normal var Normal var Very aggressive");
        transport.Respond("option name EvalFile type string default /some model/file.nnue");
        transport.Respond("option name Clear Hash type button");
        transport.Respond("uciok extra");
        Assert.Single(transport.Commands);
        transport.Respond("uciok");

        Assert.Equal("isready", await transport.ReadCommandAsync(deadline.Token));
        Assert.False(initialize.IsCompleted);
        transport.Respond("readyok extra");
        transport.Respond("info string loading");
        transport.Respond("readyok");

        var info = await initialize;
        Assert.Collection(transport.Commands,
            command => Assert.Equal("uci", command),
            command => Assert.Equal("isready", command));
        Assert.Equal("Stockfish 18", info.Name);
        Assert.Equal("the Stockfish developers", info.Author);
        Assert.Equal("spin", info.Options["Threads"].Type);
        Assert.Equal("1", info.Options["Threads"].DefaultValue);
        Assert.Equal("1", info.Options["Threads"].Minimum);
        Assert.Equal("1024", info.Options["Threads"].Maximum);
        Assert.Equal("false", info.Options["UCI Chess960"].DefaultValue);
        Assert.Collection(info.Options["Evaluation style"].Variables,
            value => Assert.Equal("Normal", value),
            value => Assert.Equal("Very aggressive", value));
        Assert.Equal("Normal", info.Options["Evaluation style"].DefaultValue);
        Assert.Equal("/some model/file.nnue", info.Options["EvalFile"].DefaultValue);
        Assert.Null(info.Options["Clear Hash"].DefaultValue);
    }

    [Fact]
    public async Task InitializeAsync_PreservesUnknownOptionTypesAndOptionalAuthor()
    {
        await using var transport = new FakeUciTransport();
        transport.Respond("id name Future Engine");
        transport.Respond("id unknown metadata");
        transport.Respond("option name Experimental Feature type future default some value");
        transport.Respond("option name Empty type string default");
        transport.Respond("uciok");
        transport.Respond("readyok");

        var info = await new UciSession(transport).InitializeAsync(Options());

        Assert.Null(info.Author);
        Assert.Equal("future", info.Options["Experimental Feature"].Type);
        Assert.Equal("some value", info.Options["Experimental Feature"].DefaultValue);
        Assert.Equal(string.Empty, info.Options["Empty"].DefaultValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializeAsync_RejectsEndOfStreamBeforeAcknowledgement(bool gotUciOk)
    {
        await using var transport = new FakeUciTransport();
        transport.Respond("id name Stockfish");
        if (gotUciOk)
        {
            transport.Respond("uciok");
        }
        transport.CompleteOutput();

        var exception = await Assert.ThrowsAsync<UciProtocolException>(
            () => new UciSession(transport).InitializeAsync(Options()));

        Assert.Contains(gotUciOk ? "readyok" : "uciok", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializeAsync_TimesOutWhenAcknowledgementNeverArrives(bool gotUciOk)
    {
        await using var transport = new FakeUciTransport();
        transport.Respond("id name Stockfish");
        if (gotUciOk)
        {
            transport.Respond("uciok");
        }

        transport.Respond(gotUciOk ? "readyok extra" : "uciok extra");

        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => new UciSession(transport).InitializeAsync(Options(TimeSpan.FromMilliseconds(100))));

        Assert.Contains(gotUciOk ? "readyok" : "uciok", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("id name")]
    [InlineData("id author")]
    [InlineData("option")]
    [InlineData("option name Threads type default 1")]
    [InlineData("option name Threads")]
    [InlineData("option name type spin")]
    [InlineData("option name Threads type")]
    [InlineData("option name Threads type spin min")]
    [InlineData("option name Style type combo var")]
    public async Task InitializeAsync_PreservesMalformedProtocolLine(string line)
    {
        await using var transport = new FakeUciTransport();
        transport.Respond(line);
        transport.CompleteOutput();

        var exception = await Assert.ThrowsAsync<UciProtocolException>(
            () => new UciSession(transport).InitializeAsync(Options()));

        Assert.Equal(line, exception.Line);
        Assert.Contains(line, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitializeAsync_RejectsMissingEngineName()
    {
        await using var transport = new FakeUciTransport();
        transport.Respond("uciok");

        var exception = await Assert.ThrowsAsync<UciProtocolException>(
            () => new UciSession(transport).InitializeAsync(Options()));

        Assert.Equal("uciok", exception.Line);
        Assert.Single(transport.Commands);
    }

    [Fact]
    public async Task InitializeAsync_PropagatesCallerCancellationRatherThanTimeout()
    {
        await using var transport = new FakeUciTransport();
        using var cancellation = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var initialize = new UciSession(transport).InitializeAsync(Options(), cancellation.Token);

        Assert.Equal("uci", await transport.ReadCommandAsync(deadline.Token));
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialize);
        Assert.Single(transport.Commands);
    }

    [Fact]
    public async Task InitializeAsync_LeavesSubsequentLinesAvailable()
    {
        await using var transport = new FakeUciTransport();
        transport.Respond("id name Stockfish");
        transport.Respond("uciok");
        transport.Respond("readyok");
        transport.Respond("info string next response");

        await new UciSession(transport).InitializeAsync(Options());

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var lines = transport.ReadLinesAsync(deadline.Token).GetAsyncEnumerator(deadline.Token);
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("info string next response", lines.Current);
    }

    [Fact]
    public async Task InitializeAsync_DoesNotWriteWhenCallerAlreadyCancelled()
    {
        await using var transport = new FakeUciTransport();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new UciSession(transport).InitializeAsync(Options(), cancellation.Token));

        Assert.Empty(transport.Commands);
    }
}
