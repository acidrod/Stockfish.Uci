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

    [Fact]
    public void Parse_RejectsNullToken()
    {
        Assert.Throws<ArgumentNullException>(() => UciMove.Parse(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("e2-e4")]
    [InlineData("e9e4")]
    [InlineData("e7e8x")]
    [InlineData("e2e4\n")]
    [InlineData("e2e4\r\n")]
    [InlineData("e2e4\0")]
    [InlineData("e2e4 ")]
    [InlineData("e2e4\nquit")]
    [InlineData("0000\n")]
    public void Parse_RejectsMalformedTokens(string token)
    {
        Assert.Throws<FormatException>(() => UciMove.Parse(token));
    }
}
