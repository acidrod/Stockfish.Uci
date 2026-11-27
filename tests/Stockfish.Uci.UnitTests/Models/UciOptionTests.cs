using Xunit;

namespace Stockfish.Uci.UnitTests.Models;

public sealed class UciOptionTests
{
    [Fact]
    public void Constructor_PreservesUnknownOptionType()
    {
        var option = new UciOption("Future option", "future", "default", null, null, ["first", "second"]);

        Assert.Equal("Future option", option.Name);
        Assert.Equal("future", option.Type);
        Assert.Equal("default", option.DefaultValue);
        Assert.Null(option.Minimum);
        Assert.Null(option.Maximum);
        Assert.Collection(option.Variables,
            value => Assert.Equal("first", value),
            value => Assert.Equal("second", value));
    }
}
