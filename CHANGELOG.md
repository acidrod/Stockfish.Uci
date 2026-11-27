# Changelog

Changes to Stockfish.Uci are recorded here. Versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.0]

### Added

- Managed .NET 10 UCI client for an explicitly installed Stockfish executable.
- Typed engine identity, options, moves, evaluations, principal variations and analysis results.
- Bounded and streaming analysis, MultiPV, cancellation and process lifecycle management.
- Standalone BasicAnalysis sample and cross-platform external engine installation documentation.
- Unit tests and a separate real-engine integration suite, with GitHub Actions test reports.
- Managed-only NuGet package verification, consumer smoke test and separate immutable release and trusted publication workflows.
- Release note categories and contributor, security and release documentation.

### Compatibility

- Targets .NET 10.
- Engine binaries are not included; users choose and install their own Stockfish executable.
- Platform compatibility is limited to the configurations covered by the linked release validation runs.
