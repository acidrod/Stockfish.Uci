# Security policy

## Reporting a vulnerability

Use [GitHub private vulnerability reporting](https://github.com/acidrod/Stockfish.Uci/security/advisories/new) to report a suspected security issue affecting Stockfish.Uci. Do not disclose the vulnerability, exploit or sensitive diagnostic information in a public issue or pull request.

Include, where available:

- The affected Stockfish.Uci version or commit SHA.
- Operating system, .NET SDK/runtime and external engine version.
- Steps to reproduce, expected behavior and observed security consequences.
- Relevant logs or a minimal reproduction with credentials and personal data removed.

The maintainer will review reports and coordinate disclosure through GitHub. No response-time guarantee is made.

## Scope and supported versions

Security reports against the current release or the `main` branch are welcome. Older releases may require upgrading; this project does not promise long-term security backports.

Stockfish.Uci starts an executable that the caller explicitly supplies. Only use engine executables obtained from trusted sources and verify downloads where possible. Do not pass paths or engine options from untrusted input without application-level validation.

Stockfish itself is a separate upstream project. For vulnerabilities in Stockfish rather than this wrapper, follow the [upstream security policy](https://github.com/official-stockfish/Stockfish/security).

## Release integrity

Published releases use immutable GitHub tags and assets. The release process verifies the package contents, source commit, test evidence, asset attestations and SHA-256 checksum before trusted NuGet publication. See the [release procedure](docs/releasing.md) for details.
