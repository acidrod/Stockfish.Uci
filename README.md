# Stockfish.Uci

> **Work in progress.** A modern, strongly typed .NET client for Universal Chess Interface (UCI) engines, designed to provide a simple analysis API backed by Stockfish 18.

`Stockfish.Uci` will package a .NET API and, for each explicitly supported runtime, a verified Stockfish binary. It will transform UCI's line-based protocol into typed objects such as `AnalysisResult`, `PrincipalVariation`, `Evaluation`, and `UciMove`.

## Status

The repository has been initialized. The first public release will target `linux-x64`; no engine binary is committed or distributed yet.

## Intended usage

```csharp
await using var engine = await StockfishEngine.CreateAsync();

var result = await engine.AnalyzeAsync(
    position,
    new AnalysisLimits { Depth = 18, MultiPv = 3 });

Console.WriteLine(result.BestMove);
```

The API, package name, and implementation are not stable before `v0.1.0`.

## License and third-party notices

This repository is licensed under the [GNU General Public License v3.0](LICENSE). Stockfish is also licensed under GPLv3. See [NOTICE.md](NOTICE.md) for third-party notices and the planned provenance requirements for bundled engine binaries.

This is an independent, unofficial project and is not affiliated with or endorsed by the Stockfish project.

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening an issue or pull request.
