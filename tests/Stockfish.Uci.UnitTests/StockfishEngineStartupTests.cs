using Xunit;

namespace Stockfish.Uci.UnitTests;

public sealed class StockfishEngineStartupTests
{
    [Fact]
    public async Task StartAsync_ThrowsFileNotFoundException_WhenConfiguredExecutableDoesNotExist()
    {
        var options = new StockfishEngineOptions(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => StockfishEngine.StartAsync(options));

        Assert.Equal(options.ExecutablePath, exception.FileName);
    }

    [Fact]
    public async Task StartAsync_RejectsNullOptions()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => StockfishEngine.StartAsync(null!));
    }

    [Fact]
    public async Task StartAsync_HonorsAlreadyCancelledToken()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var options = new StockfishEngineOptions(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => StockfishEngine.StartAsync(options, cancellation.Token));
    }

    [Fact]
    public async Task StartAsync_ReportsConfiguredPathWhenProcessCannotStart()
    {
        var path = Path.GetTempFileName();
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => StockfishEngine.StartAsync(new StockfishEngineOptions(path)));

            Assert.Contains(path, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
