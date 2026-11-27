using Xunit;

namespace Stockfish.Uci.UnitTests.Models;

public sealed class EvaluationTests
{
    [Theory]
    [InlineData(EvaluationKind.Centipawns, 34)]
    [InlineData(EvaluationKind.Mate, -3)]
    public void Constructor_PreservesEngineScore(EvaluationKind kind, int value)
    {
        var evaluation = new Evaluation(kind, value);

        Assert.Equal(kind, evaluation.Kind);
        Assert.Equal(value, evaluation.Value);
    }
}
