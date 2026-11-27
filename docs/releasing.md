# Releasing Stockfish.Uci

Release creation, test execution and NuGet publication are deliberately separate workflows. No push or pull request publishes a package.

## One-time repository configuration

1. In GitHub **Settings → General → Releases**, enable **release immutability** before publishing your first release. This freezes the released tag and assets and supplies a release attestation. Drafts remain editable.
2. In GitHub **Settings → Environments**, create an environment named `nuget`. Limit deployment branches to `main`; configure required reviewer approval if available. Add the environment secret `NUGET_USER` containing the NuGet.org profile username (not the email or an API key).
3. Sign in to NuGet.org and open **Trusted Publishing**. Add a GitHub policy:

   | Field | Value |
   | --- | --- |
   | Repository owner | acidrod |
   | Repository | Stockfish.Uci |
   | Workflow file | publish.yml |
   | Environment | nuget |
   | Package pattern | Stockfish.Uci |

   Allow new packages and new versions for the initial publication; after the package exists, restrict to publishing new versions. Do not grant unlisting permissions. No long-lived NuGet API key is needed. The environment secret stores only the account username; never store a NuGet password or API key in the repository.

## Release notes and labels

Maintain the user-facing changes in [CHANGELOG.md](../CHANGELOG.md). Apply the appropriate [PR labels](../CONTRIBUTING.md#labels); GitHub groups automatically generated notes according to [the release configuration](../.github/release.yml). Breaking changes and security fixes take precedence. Use `skip-changelog` for internal changes that should be omitted.

## Prepare a version

- Use Semantic Versioning. Update `<Version>` in `src/Stockfish.Uci/Stockfish.Uci.csproj` and add a matching `## [version]` section in `CHANGELOG.md` through a reviewed PR.
- Pre-release versions such as `0.2.0-rc.1` are supported. Use lowercase prerelease identifiers and omit build metadata.
- Merge the version PR, wait for **Managed CI** on `main`, then manually run **Stockfish integration** on `main`. Both workflows must succeed on the exact same commit. Change nothing on `main` between validation and release preparation, or validate the new commit.
- In Actions, choose **Prepare GitHub release** → Run workflow, select `main`, enter the version matching the library project (for example `0.1.1`), and leave `create_draft` unchecked for a rehearsal. The workflow verifies the version, validation runs and package metadata; builds only the library; checks its contents; creates a SHA-256 manifest; and restores and executes a consumer using only the local package.
- When the rehearsal is accepted, run it again with `create_draft` checked (the package is built and validated again in that run). A draft GitHub release is created with release notes, commit SHA, validation links, the `.nupkg` and `SHA256SUMS`. Its tag targets the exact validated commit. No NuGet package is published.
- Review the draft, its assets and version. Publish the GitHub release yourself. Verify that GitHub marks the release **Immutable**. Do not manually create or retarget the tag; an existing tag is rejected by preparation.

## Publish the exact package to NuGet.org

1. Choose **Publish NuGet** → Run workflow on `main`, and enter the published GitHub release version.
2. Leave `publish_to_nuget` unchecked to validate without publication.
3. The workflow checks the immutable release, the tag's ancestry on `main`, successful CI and real-engine integration for the tag commit, both release asset attestations, package identity, managed-only contents, source commit and checksum. It does not rebuild the package.
4. If ready, rerun with `publish_to_nuget` checked and approve the `nuget` environment deployment when prompted. The publishing job uses an ephemeral API key obtained with GitHub OIDC. It sends the original verified package to NuGet.org.
5. Wait for NuGet.org validation/indexing, then complete the public-package checks below.

A version cannot be overwritten on NuGet.org. Correct a faulty publication with a new version, not a force-push or replacement asset. GitHub release immutability likewise protects published tags and assets.

## Verify the published package

Publication is complete only when the exact version is listed and a clean consumer can restore and use it from NuGet.org.

- Open the [package page](https://www.nuget.org/packages/Stockfish.Uci), select the published version and verify its identity, listing, .NET target, README and license. From v0.1.1 onward, confirm that the package icon is displayed.
- Record the successful publication run and the public package version URL. Keep the release's existing validation links as evidence for the exact source commit.

Run the following Bash smoke test on a machine with the .NET 10 SDK. Set `version` to the **published** version being verified. It does not require Stockfish or a source checkout.

```bash
(
  set -euo pipefail
  version=0.1.1
  work=$(mktemp -d)
  trap 'rm -rf "$work"' EXIT
  cd "$work"
  export NUGET_PACKAGES="$work/packages"

  dotnet new console --framework net10.0 --name PackageConsumer \
    --output PackageConsumer --no-restore
  dotnet add PackageConsumer/PackageConsumer.csproj package Stockfish.Uci \
    --version "$version" --no-restore

  cat > PackageConsumer/Program.cs <<'CS'
using Stockfish.Uci;

var move = UciMove.Parse("e2e4");
if (move.Value != "e2e4")
{
    throw new InvalidOperationException("Unexpected result from the installed package.");
}

Console.WriteLine($"Package smoke test passed: {move}");
CS

  dotnet restore PackageConsumer/PackageConsumer.csproj \
    --source https://api.nuget.org/v3/index.json --no-http-cache
  dotnet run --project PackageConsumer/PackageConsumer.csproj \
    --configuration Release --no-restore
)
```

The isolated global-packages directory and explicit public feed prevent a previously cached or local development package from satisfying the restore. The test checks consumption of the managed library; it is not a substitute for the release integration runs. Record its result in the release work item. If the package has not appeared yet, wait for validation/indexing rather than attempting another push. For incompatible changes or packaging corrections, prepare a new version.

## References

- [NuGet Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)
- [GitHub immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases)
- [Verify release integrity](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/secure-your-dependencies/verify-release-integrity)
