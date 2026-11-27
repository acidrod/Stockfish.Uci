# Changelog

Changes to Stockfish.Uci are recorded here. Versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.1]

### Added

- Original chess-analysis package icon embedded in the NuGet package.
- Compatibility policy with release-specific managed CI and real-engine integration evidence.

### Changed

- Package verification now requires the exact icon file and validates its metadata, PNG dimensions and size without relaxing native-binary restrictions.
- Updated installation documentation and added a clean public-feed consumer verification procedure for published versions.
- NuGet release notes now show the exact version’s changelog and a link to its GitHub release, rather than only a generic releases URL.

### Compatibility

- No public API or engine behavior changes; the target remains .NET 10 and the engine remains externally installed.
- Consult the release validation links for the configurations verified for this version.

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
