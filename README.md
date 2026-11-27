# Stockfish.Uci

[![Managed CI](https://github.com/acidrod/Stockfish.Uci/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/acidrod/Stockfish.Uci/actions/workflows/ci.yml)
[![Stockfish integration](https://github.com/acidrod/Stockfish.Uci/actions/workflows/integration.yml/badge.svg?branch=main)](https://github.com/acidrod/Stockfish.Uci/actions/workflows/integration.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue)](https://github.com/acidrod/Stockfish.Uci/blob/main/LICENSE)

A strongly typed .NET client for Universal Chess Interface (UCI) engines, designed for analysis with a user-installed Stockfish executable.

[NuGet](https://www.nuget.org/packages/Stockfish.Uci) · [Releases](https://github.com/acidrod/Stockfish.Uci/releases) · [Changelog](https://github.com/acidrod/Stockfish.Uci/blob/main/CHANGELOG.md) · [Issues](https://github.com/acidrod/Stockfish.Uci/issues)

## Features

- Start and manage an external UCI engine process.
- Discover engine identity and supported options.
- Analyze positions with depth and time limits, including MultiPV.
- Stream analysis updates, cancel searches and dispose of the engine asynchronously.
- Work with typed moves, evaluations, principal variations and analysis results.

The NuGet package contains **managed .NET code only**. It does not download, bundle, redistribute, select or update Stockfish binaries. Your application remains responsible for installing and licensing its engine executable.

## Getting started

### Requirements

- .NET 10.
- A separately installed Stockfish executable appropriate for your operating system and CPU.

Follow the [Stockfish installation guide](https://github.com/acidrod/Stockfish.Uci/blob/main/docs/installing-stockfish.md) to locate and verify your engine. Consult the [compatibility policy](https://github.com/acidrod/Stockfish.Uci/blob/main/docs/compatibility.md) for tested configurations.

### Install the library

Install the latest listed stable version from NuGet.org:

```bash
dotnet add package Stockfish.Uci
```

Check [NuGet.org](https://www.nuget.org/packages/Stockfish.Uci) and [GitHub Releases](https://github.com/acidrod/Stockfish.Uci/releases) for available versions. Use `--version` to select a specific published version. For unpublished changes, build the library from source or use the local package instructions below.

### Analyze a position

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

Replace the executable path with your installed engine's absolute path. Use `startpos` or `fen <six FEN fields>` with optional UCI moves, without the `position` prefix. Set a depth, move time or both. Use `AnalyzeStreamingAsync` for live updates; pass a cancellation token to stop a search. Only one search may run on an engine instance at a time.

### Run the sample from source

From the repository root, run the standalone, non-packable BasicAnalysis console project.

Linux or macOS:

```bash
dotnet run --project samples/BasicAnalysis/BasicAnalysis.csproj -- /absolute/path/to/stockfish
```

Windows (PowerShell):

```powershell
dotnet run --project samples/BasicAnalysis/BasicAnalysis.csproj -- 'C:\Tools\Stockfish\stockfish-windows-x86-64.exe'
```

The sample prints the engine name, best move and principal variations.

## Quality and platform validation

| Workflow | Scope | Trigger |
| --- | --- | --- |
| [Managed CI](https://github.com/acidrod/Stockfish.Uci/actions/workflows/ci.yml) | Unit tests once on Linux; then build, formatting and package validation on Windows, Linux and macOS | PRs and pushes to `main` |
| [Stockfish integration](https://github.com/acidrod/Stockfish.Uci/actions/workflows/integration.yml) | Real-engine tests using official, checksum-verified Stockfish 18 assets on Windows, Linux and macOS | Manual and weekly |

Unit tests require no installed engine. Stockfish 18 integration tests passed on Linux x64, Windows x64 and macOS ARM64 for v0.1.0. See the [compatibility policy](https://github.com/acidrod/Stockfish.Uci/blob/main/docs/compatibility.md#recorded-release-evidence) and each release’s validation links; evidence from one release does not certify later releases or other configurations. CI downloads an engine only into temporary integration-runner directories and does not publish it as an artifact.

The unit-test job and each integration job include a GitHub Actions summary with pass/fail/skip counts, duration and expandable individual results.

## Build and inspect the local package

```bash
dotnet pack src/Stockfish.Uci/Stockfish.Uci.csproj --configuration Release --output artifacts/packages
python3 scripts/verify-package.py --package artifacts/packages/Stockfish.Uci.0.1.1.nupkg
```

Only the library is packable. The package contains its managed assembly, README, original package icon (from v0.1.1) and NuGet metadata; samples, tests and engine executables are excluded. The verifier checks the exact package icon and the version-specific release notes generated from CHANGELOG.md, and rejects unexpected package entries, including native binaries. Use the version specified by the library project when naming the package.

## Documentation

- [Installing Stockfish](https://github.com/acidrod/Stockfish.Uci/blob/main/docs/installing-stockfish.md)
- [Compatibility policy](https://github.com/acidrod/Stockfish.Uci/blob/main/docs/compatibility.md)
- [Contributing and local validation](https://github.com/acidrod/Stockfish.Uci/blob/main/CONTRIBUTING.md)
- [Security policy](https://github.com/acidrod/Stockfish.Uci/blob/main/SECURITY.md)
- [Changelog](https://github.com/acidrod/Stockfish.Uci/blob/main/CHANGELOG.md)

## License and notices

This project is licensed under the [GNU General Public License v3.0](https://github.com/acidrod/Stockfish.Uci/blob/main/LICENSE). Stockfish is independently licensed under GPLv3; this package does not include Stockfish code or binaries. See [third-party notices](https://github.com/acidrod/Stockfish.Uci/blob/main/NOTICE.md).

This is an independent, unofficial project and is not affiliated with or endorsed by the Stockfish project.
