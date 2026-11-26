using System.Reflection;
using Xunit;

namespace Stockfish.Uci.UnitTests;

public sealed class ProjectReferenceTests
{
    [Fact]
    public void UnitTestProjectCanLoadLibraryAssembly()
    {
        var assembly = Assembly.Load("Stockfish.Uci");

        Assert.Equal("Stockfish.Uci", assembly.GetName().Name);
    }
}
