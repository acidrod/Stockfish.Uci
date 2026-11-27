# Contributing to Stockfish.Uci

## Development setup

Use the .NET SDK specified in [global.json](global.json). Python 3 is required for the package verification and release scripts. Stockfish is only required for real-engine integration tests and the console sample; see [installation instructions](docs/installing-stockfish.md).

## Workflow

1. Open a focused [work item](https://github.com/acidrod/Stockfish.Uci/issues/new/choose) with a concrete objective and acceptance criteria.
2. Create a branch from `main`, for example `feature/12-engine-options` or `fix/13-parser`.
3. Keep the change focused and add tests for behavior changes or bug fixes.
4. Update the README, relevant documentation and the `Unreleased` changelog section for user-visible changes.
5. Open a pull request against `main`, link the issue with `Closes #12` where applicable, and describe the validation performed.

Keep the public API strongly typed and independent from raw UCI strings. Tolerate unknown protocol fields while treating malformed required fields explicitly. Do not claim compatibility with an engine version, runtime or CPU variant without end-to-end evidence.

Do not commit build output, local packages, credentials, downloaded archives or executable artifacts.

## Labels

Use the existing GitHub labels to classify issues and pull requests. A PR may have multiple labels; release notes group it under its first matching category in the release configuration.

| Label | Use |
| --- | --- |
| `enhancement` | New features or improvements |
| `bug` | Defects |
| `breaking-change` | Incompatible public API or behavior changes |
| `security` | Security work; report vulnerabilities privately |
| `documentation` | Documentation |
| `release` | Versioning, releases and package publication |
| `ci` | CI and build automation |
| `tests` | Unit and integration tests |
| `dependencies` | Dependency or SDK updates |
| `maintenance` | Refactoring and repository maintenance |
| `skip-changelog` | PRs intentionally omitted from generated release notes |

See the [repository labels](https://github.com/acidrod/Stockfish.Uci/labels) for the full list. Use `skip-changelog` only for changes that should not appear in the generated notes.

## Local validation

From the repository root:

```bash
dotnet restore Stockfish.Uci.sln
dotnet build Stockfish.Uci.sln --configuration Release --no-restore
dotnet test tests/Stockfish.Uci.UnitTests/Stockfish.Uci.UnitTests.csproj --configuration Release --no-build
dotnet format Stockfish.Uci.sln --verify-no-changes --severity warn --no-restore
python3 -m unittest discover -s .github/scripts -p 'test_release_tools.py'
```

For changes to engine startup, protocol or analysis, also run the integration tests against your installed Stockfish (Linux or macOS example):

```bash
STOCKFISH_UCI_PATH=/absolute/path/to/stockfish dotnet test tests/Stockfish.Uci.IntegrationTests/Stockfish.Uci.IntegrationTests.csproj --configuration Release --no-build
```

On Windows, set the environment variable as shown in the [installation guide](docs/installing-stockfish.md). Without an engine, the integration tests are skipped, not passed.

Before a packaging change, also build and verify the managed-only package using the [README](README.md#build-and-inspect-the-local-package).

Managed CI runs on pull requests; the real-engine workflow runs separately on demand and weekly. Both workflows must pass on the exact release commit before a release can be prepared. Release creation and NuGet publication are handled by the project maintainer; contributors do not need publishing access.

## Security

Please report suspected vulnerabilities using the [security policy](SECURITY.md), rather than a public issue.
