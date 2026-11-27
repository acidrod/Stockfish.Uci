# Third-party notices

## Stockfish

[Stockfish](https://stockfishchess.org/) is a chess engine distributed under the GNU General Public License version 3 (GPL-3.0). Copyright belongs to the Stockfish developers; see the upstream project for its complete copyright notices.

**No Stockfish binary, source code or other Stockfish artifact is included in the repository, NuGet package or release assets.** The integration workflow obtains official Stockfish release assets in temporary runner directories solely to execute tests; it does not publish those assets.

Users obtain and manage their own Stockfish installation. The package communicates with the user-supplied executable through the published UCI text protocol.

The project must not imply affiliation with the Stockfish project. If a future change proposes distributing an upstream binary or source artifact, it requires a separate issue, licensing review, provenance documentation, and explicit approval before implementation.
