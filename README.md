# Stockfish.Uci

> **Work in progress.** A modern, strongly typed .NET client for Universal Chess Interface (UCI) engines, designed for analysis with a user-installed Stockfish executable.

`Stockfish.Uci` converts the line-based UCI protocol into typed .NET objects such as `AnalysisResult`, `PrincipalVariation`, `Evaluation`, and `UciMove`.

## What this package does—and does not—contain

This package contains managed .NET code only. It does **not** download, bundle, redistribute, select, or update Stockfish binaries.

Install Stockfish yourself and pass its absolute executable path when creating the client. See [installing Stockfish](docs/installing-stockfish.md).

## Status

The managed .NET 10 wrapper implements engine startup, UCI option discovery, bounded analysis, MultiPV, streaming, cancellation and process cleanup. Version 0.1.0 is under development and has not been published to NuGet.org.

## Platform testing

[Managed CI](https://github.com/acidrod/Stockfish.Uci/actions/workflows/ci.yml) builds the solution, verifies formatting and checks the managed-only NuGet package on Windows, Linux and macOS. A separate Unit tests job runs the unit test project once on Linux, without configuring a Stockfish executable. The integration tests are not discovered or counted there.

Local real-engine integration tests have passed with Stockfish 18 on Linux. The separate [Stockfish integration workflow](https://github.com/acidrod/Stockfish.Uci/actions/workflows/integration.yml) is configured for manual and weekly runs on the three operating systems using official, checksum-verified Stockfish release assets in temporary runner directories. It does not run on pull requests or publish engine binaries. Cross-platform real-engine CI verification is still pending; the manual workflow becomes available when its definition reaches the default branch.

The unit test job and each platform-specific integration job publish a test report on their GitHub Actions run summary, with pass/fail/skip counts, duration and expandable individual test results.

Your application remains responsible for selecting, installing and licensing its engine binary.

## Quick start

1. Install Stockfish as described in [installing Stockfish](docs/installing-stockfish.md).
2. Pass its absolute executable path to `StockfishEngineOptions`.
3. From the repository root, run the separate, non-packable console sample.

Linux or macOS (replace the path with your installed executable):

```bash
dotnet run --project samples/BasicAnalysis/BasicAnalysis.csproj -- /absolute/path/to/stockfish
```

Windows (PowerShell):

```powershell
dotnet run --project samples/BasicAnalysis/BasicAnalysis.csproj -- 'C:\Tools\Stockfish\stockfish-windows-x86-64.exe'
```

The sample prints the engine name, the best move, and its principal variations. The engine remains external to the application and package.

## Library usage

```csharp
using Stockfish.Uci;

await using var engine = await StockfishEngine.StartAsync(
    new StockfishEngineOptions("/usr/bin/stockfish"));

var result = await engine.AnalyzeAsync(
    "startpos moves e2e4 e7e5",
    new AnalysisLimits { Depth = 18, MultiPv = 3 });

Console.WriteLine(result.BestMove);
foreach (var variation in result.Variations)
{
    Console.WriteLine($"PV {variation.Index}: {string.Join(' ', variation.Moves)}");
}
```

Use `startpos` or `fen <six FEN fields>` with optional UCI moves, without the `position` prefix. Set a depth, move time, or both. Use `AnalyzeStreamingAsync` for live updates; pass a cancellation token to stop a search. Only one search may run on an engine instance at a time.

## Build and inspect the local NuGet package

From the repository root:

```bash
dotnet pack src/Stockfish.Uci/Stockfish.Uci.csproj --configuration Release --output artifacts/packages
python3 scripts/verify-package.py --package artifacts/packages/Stockfish.Uci.0.1.0.nupkg
```

Only the library is packable. The package contains its managed assembly, README and NuGet metadata; the sample, tests and engine executables are excluded. The verifier rejects unexpected package entries.

## License and third-party notices

This repository is licensed under the [GNU General Public License v3.0](LICENSE). Stockfish is independently licensed under GPLv3; this package does not include Stockfish code or binaries. See [NOTICE.md](NOTICE.md).

This is an independent, unofficial project and is not affiliated with or endorsed by the Stockfish project.

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening an issue or pull request.
