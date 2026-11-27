# Installing and configuring Stockfish

`Stockfish.Uci` does not include a chess engine. Install Stockfish or another compatible UCI engine yourself, then supply its absolute executable path to `StockfishEngineOptions`.

## Obtain Stockfish

Use the official Stockfish download page or your operating system's trusted package manager:

- Official releases: <https://stockfishchess.org/download/>
- Source and release history: <https://github.com/official-stockfish/Stockfish>

The exact package-manager version varies by operating system and repository. The commands below verify the executable actually selected; they are more important than assuming a package name implies a particular Stockfish version.

## Windows

Download and extract the official Windows archive to a user-managed directory, for example:

```text
C:\Tools\Stockfish\stockfish-windows-x86-64.exe
```

In PowerShell, verify it starts:

```powershell
& 'C:\Tools\Stockfish\stockfish-windows-x86-64.exe' uci
```

Pass the same absolute path to `StockfishEngineOptions`.

## Linux

Install Stockfish through the distribution's package manager or from the official release. Then discover and verify the executable:

```bash
command -v stockfish
stockfish uci
```

For this development machine, the verified system path is `/usr/bin/stockfish`; another distribution may use a different location.

## macOS

Install Stockfish through a trusted package manager or official release, then discover and verify it:

```bash
command -v stockfish
stockfish uci
```

Pass the absolute result of `command -v stockfish` to `StockfishEngineOptions`.

## Verify before using from .NET

A valid UCI engine responds with identity/options followed by `uciok` after receiving `uci`, and with `readyok` after receiving `isready`. The library reports a clear startup/protocol error when either response is absent before the configured timeout.

## Version policy

The NuGet package version and the external Stockfish version are independent. The library records engine identity/options at startup and supports its UCI contract; the project documentation identifies versions that have been exercised by integration tests.
