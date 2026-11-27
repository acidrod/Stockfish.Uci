# Stockfish.Uci

> **Work in progress.** A modern, strongly typed .NET client for Universal Chess Interface (UCI) engines, designed for analysis with a user-installed Stockfish executable.

`Stockfish.Uci` converts the line-based UCI protocol into typed .NET objects such as `AnalysisResult`, `PrincipalVariation`, `Evaluation`, and `UciMove`.

## What this package does—and does not—contain

This package contains managed .NET code only. It does **not** download, bundle, redistribute, select, or update Stockfish binaries.

Install Stockfish yourself and pass its absolute executable path when creating the client. See [installing Stockfish](docs/installing-stockfish.md).

## Status

The repository contains the .NET solution and unit-test foundation. The UCI client API is not yet stable before `v0.1.0`.

## Intended usage

```csharp
await using var engine = await StockfishEngine.StartAsync(
    new StockfishEngineOptions("/usr/bin/stockfish"));

var result = await engine.AnalyzeAsync(
    "startpos moves e2e4 e7e5",
    new AnalysisLimits { Depth = 18, MultiPv = 3 });

Console.WriteLine(result.BestMove);
```

The exact API shown above becomes valid only when the v0.1 implementation and tests in this repository exist.

## License and third-party notices

This repository is licensed under the [GNU General Public License v3.0](LICENSE). Stockfish is independently licensed under GPLv3; this package does not include Stockfish code or binaries. See [NOTICE.md](NOTICE.md).

This is an independent, unofficial project and is not affiliated with or endorsed by the Stockfish project.

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening an issue or pull request.
