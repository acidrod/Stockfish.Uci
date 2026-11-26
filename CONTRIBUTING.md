# Contributing to Stockfish.Uci

## Development principles

- Keep the public API strongly typed and independent from raw UCI strings.
- Do not claim support for an engine version, runtime, or CPU variant without an automated end-to-end test.
- Keep protocol parsing tolerant of unknown UCI fields but explicit about malformed required fields.
- Prefer small, focused pull requests with tests and documentation for public behaviour changes.

## Stockfish distribution

Do not add or replace a Stockfish binary without updating `NOTICE.md` and an engine manifest with the exact upstream version, source URL, binary hash, runtime identifier, and reproducible provenance information.

## Before opening a pull request

1. Run the relevant unit and integration tests.
2. Add tests for a behavioural change or bug fix.
3. Update the README or documentation when the public API, platform support, or licensing information changes.
4. Do not commit build output, local packages, credentials, or downloaded artifacts outside the tracked engine-distribution structure.

## Local validation

From the repository root, use the pinned SDK and run:

```bash
dotnet restore Stockfish.Uci.sln
dotnet build Stockfish.Uci.sln --configuration Release --no-restore
dotnet test Stockfish.Uci.sln --configuration Release --no-build
dotnet format Stockfish.Uci.sln --verify-no-changes --severity warn
```

Every command must exit with code `0` before opening a pull request.
