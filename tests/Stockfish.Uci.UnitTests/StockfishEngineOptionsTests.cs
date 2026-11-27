using Xunit;

namespace Stockfish.Uci.UnitTests;

public sealed class StockfishEngineOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("stockfish")]
    [InlineData("engines/stockfish")]
    public void Constructor_RejectsInvalidExecutablePath(string? path)
    {
        Assert.Throws<ArgumentException>(() => new StockfishEngineOptions(path!));
    }

    [Fact]
    public void Constructor_PreservesCallerSuppliedPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "stockfish");
        var options = new StockfishEngineOptions(path);

        Assert.Equal(path, options.ExecutablePath);
        Assert.Equal(TimeSpan.FromSeconds(10), options.StartupTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.StopTimeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4294967295)]
    public void StartupTimeout_RejectsInvalidTimeout(long milliseconds)
    {
        var path = Path.Combine(Path.GetTempPath(), "stockfish");

        Assert.Throws<ArgumentOutOfRangeException>(() => new StockfishEngineOptions(path)
        {
            StartupTimeout = TimeSpan.FromMilliseconds(milliseconds),
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4294967295)]
    public void StopTimeout_RejectsInvalidTimeout(long milliseconds)
    {
        var path = Path.Combine(Path.GetTempPath(), "stockfish");

        Assert.Throws<ArgumentOutOfRangeException>(() => new StockfishEngineOptions(path)
        {
            StopTimeout = TimeSpan.FromMilliseconds(milliseconds),
        });
    }

    [Fact]
    public void Constructor_AcceptsConfiguredTimeouts()
    {
        var options = new StockfishEngineOptions(Path.Combine(Path.GetTempPath(), "stockfish"))
        {
            StartupTimeout = TimeSpan.FromSeconds(2),
            StopTimeout = TimeSpan.FromSeconds(1),
        };

        Assert.Equal(TimeSpan.FromSeconds(2), options.StartupTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), options.StopTimeout);
    }
}
