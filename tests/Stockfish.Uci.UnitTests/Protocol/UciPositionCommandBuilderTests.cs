using Stockfish.Uci.Protocol;
using Xunit;

namespace Stockfish.Uci.UnitTests.Protocol;

public sealed class UciPositionCommandBuilderTests
{
    [Theory]
    [InlineData("startpos", "position startpos")]
    [InlineData(" startpos   moves e2e4 e7e5 ", "position startpos moves e2e4 e7e5")]
    [InlineData("fen 8/8/8/8/8/8/K7/7k w - - 0 1", "position fen 8/8/8/8/8/8/K7/7k w - - 0 1")]
    [InlineData("fen 8/8/8/8/8/8/K7/7k w - - 0 1 moves a2a3", "position fen 8/8/8/8/8/8/K7/7k w - - 0 1 moves a2a3")]
    public void Build_AcceptsPositionArguments(string position, string expected)
    {
        Assert.Equal(expected, UciPositionCommandBuilder.Build(position));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("position startpos")]
    [InlineData("startpos moves")]
    [InlineData("startpos nonsense")]
    [InlineData("startpos moves e2e9")]
    [InlineData("startpos\nquit")]
    [InlineData("startpos\0")]
    [InlineData("startpos\r")]
    [InlineData("startpos\tmoves e2e4")]
    [InlineData("fen 8/8/8/8/8/8/K7/7k w - - 0")]
    [InlineData("fen 8/8/8/8/8/8/K7/7k w - - 0 1 extra")]
    public void Build_RejectsMalformedOrInjectedPosition(string position)
    {
        Assert.Throws<ArgumentException>(() => UciPositionCommandBuilder.Build(position));
    }

    [Fact]
    public void Build_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => UciPositionCommandBuilder.Build(null!));
    }
}
