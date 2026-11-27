using Xunit;

namespace Stockfish.Uci.UnitTests;

public sealed class StockfishEngineOptionsTests
{
    [Fact]
    public void Constructor_RejectsBlankExecutablePath()
    {
        Assert.Throws<ArgumentException>(() => new StockfishEngineOptions(" "));
    }

    [Fact]
    public void Constructor_PreservesCallerSuppliedPath()
    {
        var options = new StockfishEngineOptions(@"C:\Chess\stockfish.exe");

        Assert.Equal(@"C:\Chess\stockfish.exe", options.ExecutablePath);
    }
}
