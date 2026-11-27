# Installing and configuring Stockfish

`Stockfish.Uci` does not include a chess engine. Install Stockfish or another compatible UCI engine yourself, then supply its absolute executable path to `StockfishEngineOptions`.

## Obtain Stockfish

Use the official Stockfish download page or your operating system's trusted package manager:

- Official releases: <https://stockfishchess.org/download/>
- Source and release history: <https://github.com/official-stockfish/Stockfish>

The library does not search PATH or install engines; PATH lookup in this guide is only a way to find the executable you will pass explicitly.

The exact package-manager version varies by operating system and repository. The commands below verify the executable actually selected; they are more important than assuming a package name implies a particular Stockfish version.

## Windows

Download and extract the official Windows archive to a user-managed directory, for example:

```text
C:\Tools\Stockfish\stockfish-windows-x86-64.exe
```

In PowerShell, verify it starts:

```powershell
& 'C:\Tools\Stockfish\stockfish-windows-x86-64.exe'
```

Enter `uci`, `isready`, then `quit`. Pass the same absolute path to `StockfishEngineOptions`.

From the repository root, run the sample and opt-in integration tests:

```powershell
$stockfishPath = 'C:\Tools\Stockfish\stockfish-windows-x86-64.exe'
dotnet run --project samples/BasicAnalysis/BasicAnalysis.csproj -- $stockfishPath
$env:STOCKFISH_UCI_PATH = $stockfishPath
dotnet test tests/Stockfish.Uci.IntegrationTests/Stockfish.Uci.IntegrationTests.csproj -c Release
Remove-Item Env:STOCKFISH_UCI_PATH
```

## Linux

Install Stockfish through the distribution's package manager or from the official release. Then discover and verify the executable:

```bash
command -v stockfish
stockfish
```

Enter `uci`, `isready`, then `quit`. On a system where the verified absolute path is `/usr/bin/stockfish`, run:

```bash
dotnet run --project samples/BasicAnalysis/BasicAnalysis.csproj -- /usr/bin/stockfish
STOCKFISH_UCI_PATH=/usr/bin/stockfish dotnet test tests/Stockfish.Uci.IntegrationTests/Stockfish.Uci.IntegrationTests.csproj -c Release
```

Adjust the path when your distribution installs Stockfish elsewhere.

## macOS

Install Stockfish through a trusted package manager or official release, then discover and verify it:

```bash
command -v stockfish
stockfish
```

Enter `uci`, `isready`, then `quit`. Pass the absolute result of `command -v stockfish` to the consumer:

```bash
stockfish_path="$(command -v stockfish)"
dotnet run --project samples/BasicAnalysis/BasicAnalysis.csproj -- "$stockfish_path"
STOCKFISH_UCI_PATH="$stockfish_path" dotnet test tests/Stockfish.Uci.IntegrationTests/Stockfish.Uci.IntegrationTests.csproj -c Release
```

If you installed Stockfish outside your shell's PATH, use its known absolute path instead.

## Verify before using from .NET

A valid UCI engine responds with identity/options followed by `uciok` after receiving `uci`, and with `readyok` after receiving `isready`. Check the reported `id name` as well (for the tested release of Stockfish, `id name Stockfish 18`). The library reports a clear startup/protocol error when either response is absent before the configured timeout.

## Version policy

The sample is a separate project in the Git repository, not part of the NuGet package. The integration tests require `STOCKFISH_UCI_PATH` to name a user-supplied executable; they are skipped when the variable is unset. See the [integration workflow](https://github.com/acidrod/Stockfish.Uci/actions/workflows/integration.yml) for recorded platform results.

The NuGet package version and the external Stockfish version are independent. The library records engine identity/options at startup and supports its UCI contract; the [compatibility policy](compatibility.md) identifies engine versions and platform configurations exercised by release integration tests. A newer or older engine may work, but a successful handshake alone does not establish tested compatibility.
