using Xunit;

namespace Stockfish.Uci.UnitTests.Models;

public sealed class UciMoveTests
{
    [Theory]
    [InlineData("e2e4")]
    [InlineData("e7e8q")]
    [InlineData("0000")]
    public void Parse_AcceptsValidUciTokens(string token)
    {
        var move = UciMove.Parse(token);

        Assert.Equal(token, move.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("e2-e4")]
    [InlineData("e9e4")]
    [InlineData("e7e8x")]
    public void Parse_RejectsMalformedTokens(string token)
    {
        Assert.Throws<FormatException>(() => UciMove.Parse(token));
    }
}
