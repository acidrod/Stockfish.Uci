# Compatibility policy

## Library and runtime

Stockfish.Uci targets `net10.0`. Use .NET 10 and a separately installed engine executable suitable for your operating system and CPU. The NuGet package contains the managed library and associated package documentation and metadata; starting with v0.1.1 it also includes the package icon. It contains no Stockfish executable or other native binaries.

Managed CI runs unit tests on Linux and validates the solution build, formatting and library package on Windows, Linux and macOS. Successful managed CI alone does not establish real-engine compatibility.

## External engine and protocol

The library communicates through UCI and requires an explicit absolute path to the executable. It validates the startup handshake, discovers engine identity and options, and handles analysis, MultiPV, cancellation and process lifetime. It does not install or update the engine, select its CPU-specific binary, or act as a chess legality checker.

Stockfish is the reference engine for integration testing. The integration workflow pins the official Stockfish version and verifies the selected archive's SHA-256 and engine identity before testing. Other UCI engines or Stockfish releases may work, but they are not verified solely because they implement UCI. See [Installing Stockfish](installing-stockfish.md) for configuration.

## What validation means

- **Verified:** The exact library release commit passed real-engine integration tests for the stated engine version and runner configuration.
- **Managed CI:** The managed build and package checks passed on the stated runner; this is separate from engine testing.
- **Not verified:** Other engine versions, operating systems, architectures and runtime versions without matching release evidence. In particular, the evidence does not cover every distribution, CPU or OS version.

Use each [GitHub release](https://github.com/acidrod/Stockfish.Uci/releases) to find its source commit and linked validation workflows. Do not infer compatibility of a later release from an earlier test run, or that all configurations supported by .NET and Stockfish have been tested.

## Recorded release evidence

### v0.1.0

Source commit: `df45076de2ceea7a682df4bd7b2f192d69680247`.

| Runner configuration | Managed CI | Real-engine integration (Stockfish 18) |
| --- | --- | --- |
| Linux x64 (`ubuntu-latest`) | Passed | 5/5 passed |
| Windows x64 (`windows-latest`) | Passed | 5/5 passed |
| macOS ARM64 (`macos-latest`) | Passed | 5/5 passed |

The single Linux unit-test job passed 119 tests. The integration workflow passed all 15 tests across the three runners, with no skips. The architectures identify the engine assets selected in those runs.

- [Managed CI run](https://github.com/acidrod/Stockfish.Uci/actions/runs/45932900571)
- [Stockfish integration run](https://github.com/acidrod/Stockfish.Uci/actions/runs/45933207507)
- [Immutable v0.1.0 release](https://github.com/acidrod/Stockfish.Uci/releases/tag/v0.1.0)

### Subsequent releases

The release procedure requires both workflows to pass on the exact source commit before a draft release is prepared. The published release notes link that commit's evidence. A documentation or packaging-only update follows the same validation procedure; it does not inherit test results from v0.1.0.

See the [GitHub releases](https://github.com/acidrod/Stockfish.Uci/releases) and [NuGet package versions](https://www.nuget.org/packages/Stockfish.Uci) for published artifacts and validation evidence.
