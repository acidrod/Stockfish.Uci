using Stockfish.Uci.Protocol;
using Xunit;

namespace Stockfish.Uci.UnitTests.Protocol;

public sealed class UciInfoParserTests
{
    [Fact]
    public void ParseInfo_ParsesAllRecognizedFields()
    {
        var variation = UciInfoParser.ParseInfo("info depth 18 seldepth 25 multipv 1 score cp 34 nodes 123456 nps 1000000 time 123 pv e2e4 e7e5 g1f3");

        Assert.NotNull(variation);
        Assert.Equal(1, variation.Index);
        Assert.Equal(new Evaluation(EvaluationKind.Centipawns, 34), variation.Evaluation);
        Assert.Equal(18, variation.Depth);
        Assert.Equal(25, variation.SelectiveDepth);
        Assert.Equal(123456, variation.Nodes);
        Assert.Equal(1000000, variation.NodesPerSecond);
        Assert.Equal(TimeSpan.FromMilliseconds(123), variation.Elapsed);
        Assert.Collection(variation.Moves,
            move => Assert.Equal("e2e4", move.Value),
            move => Assert.Equal("e7e5", move.Value),
            move => Assert.Equal("g1f3", move.Value));
    }

    [Fact]
    public void ParseInfo_ParsesNegativeMateAndMultiPv()
    {
        var variation = UciInfoParser.ParseInfo("info depth 24 multipv 2 score mate -3 pv d2d4 d7d5");

        Assert.NotNull(variation);
        Assert.Equal(2, variation.Index);
        Assert.Equal(new Evaluation(EvaluationKind.Mate, -3), variation.Evaluation);
    }

    [Theory]
    [InlineData("info depth 12 currmove e2e4 currmovenumber 1 string ignored diagnostic")]
    [InlineData("info string depth wrong pv rubbish")]
    [InlineData("id name Stockfish")]
    [InlineData("info nodes 10 nps 5")]
    public void ParseInfo_IgnoresInformationalLines(string line)
    {
        Assert.Null(UciInfoParser.ParseInfo(line));
    }

    [Fact]
    public void ParseInfo_ToleratesUnrecognizedFieldsAndScoreBounds()
    {
        var variation = UciInfoParser.ParseInfo("info depth 4 futurefield 99 score cp 20 lowerbound wdl 1 2 3 pv e2e4");

        Assert.NotNull(variation);
        Assert.Equal(1, variation.Index);
        Assert.Equal(new Evaluation(EvaluationKind.Centipawns, 20), variation.Evaluation);
        Assert.Single(variation.Moves);
    }

    [Theory]
    [InlineData("info depth wrong")]
    [InlineData("info score cp nope")]
    [InlineData("info score mate")]
    [InlineData("info multipv 0 score cp 1")]
    [InlineData("info time -1 score cp 1")]
    [InlineData("info nodes 9223372036854775808")]
    [InlineData("info time 9223372036854775807")]
    [InlineData("info pv e2e4 nonsense")]
    [InlineData("info pv")]
    public void ParseInfo_PreservesMalformedLine(string line)
    {
        var exception = Assert.Throws<UciProtocolException>(() => UciInfoParser.ParseInfo(line));

        Assert.Equal(line, exception.Line);
    }

    [Theory]
    [InlineData("bestmove e2e4 ponder e7e5", "e2e4", "e7e5")]
    [InlineData("bestmove e7e8q", "e7e8q", null)]
    [InlineData("bestmove 0000", "0000", null)]
    [InlineData("bestmove (none)", "0000", null)]
    public void ParseBestMove_ParsesOptionalPonder(string line, string expectedBest, string? expectedPonder)
    {
        var (best, ponder) = UciInfoParser.ParseBestMove(line);

        Assert.Equal(expectedBest, best.Value);
        Assert.Equal(expectedPonder, ponder?.Value);
    }

    [Theory]
    [InlineData("bestmove")]
    [InlineData("bestmove e2e9")]
    [InlineData("bestmove e2e4 ponder")]
    [InlineData("bestmove e2e4 ponder invalid")]
    [InlineData("bestmove e2e4 extra")]
    public void ParseBestMove_RejectsMalformedLine(string line)
    {
        Assert.Equal(line, Assert.Throws<UciProtocolException>(() => UciInfoParser.ParseBestMove(line)).Line);
    }
}
