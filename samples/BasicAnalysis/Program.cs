using Stockfish.Uci;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run -- <absolute-path-to-stockfish>");
    return 2;
}

await using var engine = await StockfishEngine.StartAsync(new StockfishEngineOptions(args[0]));
var result = await engine.AnalyzeAsync(
    "startpos moves e2e4 e7e5",
    new AnalysisLimits { Depth = 12 });

Console.WriteLine($"Engine: {engine.Info.Name}");
Console.WriteLine($"Best move: {result.BestMove}");
foreach (var variation in result.Variations)
{
    Console.WriteLine($"PV {variation.Index}: {string.Join(' ', variation.Moves)}");
}

return 0;
