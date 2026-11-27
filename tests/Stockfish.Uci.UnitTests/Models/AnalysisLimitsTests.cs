using Xunit;

namespace Stockfish.Uci.UnitTests.Models;

public sealed class AnalysisLimitsTests
{
    [Fact]
    public void Validate_RejectsLimitsWithoutABound()
    {
        var limits = new AnalysisLimits();

        Assert.Throws<ArgumentException>(limits.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RejectsNonPositiveDepth(int depth)
    {
        var limits = new AnalysisLimits { Depth = depth };

        Assert.Throws<ArgumentOutOfRangeException>(limits.Validate);
    }

    [Fact]
    public void Validate_RejectsNonPositiveMultiPv()
    {
        var limits = new AnalysisLimits { Depth = 1, MultiPv = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(limits.Validate);
    }

    [Fact]
    public void Validate_AcceptsPositiveDepthAndMultiPv()
    {
        var limits = new AnalysisLimits { Depth = 8, MultiPv = 2 };

        limits.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RejectsNonPositiveMoveTime(int milliseconds)
    {
        var limits = new AnalysisLimits { MoveTime = TimeSpan.FromMilliseconds(milliseconds) };

        Assert.Throws<ArgumentOutOfRangeException>(limits.Validate);
    }

    [Fact]
    public void Validate_AcceptsMoveTimeWithoutDepth()
    {
        var limits = new AnalysisLimits { MoveTime = TimeSpan.FromMilliseconds(250) };

        limits.Validate();
        Assert.Equal(1, limits.MultiPv);
    }

    [Fact]
    public void Validate_AcceptsBothBounds()
    {
        var limits = new AnalysisLimits { Depth = 18, MoveTime = TimeSpan.FromSeconds(5) };

        limits.Validate();
    }
}
