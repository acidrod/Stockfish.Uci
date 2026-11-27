#!/usr/bin/env python3
"""Check release readiness and seal/verify the exact managed NuGet artifact."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from zipfile import BadZipFile, ZipFile

PACKAGE_ID = "Stockfish.Uci"
PROJECT = Path("src/Stockfish.Uci/Stockfish.Uci.csproj")
VERSION_PATTERN = re.compile(
    r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)"
    r"(?:-[0-9a-z-]+(?:\.[0-9a-z-]+)*)?"
)
COMMIT_PATTERN = re.compile(r"[0-9a-f]{40}")


def validate_version(version: str) -> str:
    if not VERSION_PATTERN.fullmatch(version):
        raise ValueError("Use a normalized SemVer version, e.g. 0.1.0 or 0.2.0-rc.1")
    if "-" in version:
        for part in version.split("-", 1)[1].split("."):
            if part.isdigit() and len(part) > 1 and part.startswith("0"):
                raise ValueError("Numeric prerelease identifiers cannot have leading zeroes")
    return version


def validate_commit(commit: str) -> str:
    if not COMMIT_PATTERN.fullmatch(commit):
        raise ValueError("Expected a full 40-character Git commit SHA")
    return commit


def require_evidence(repo: str, commit: str) -> list[tuple[str, str]]:
    validate_commit(commit)
    evidence = []
    for workflow, label, events in (
        ("ci.yml", "Managed CI", {"push"}),
        ("integration.yml", "Stockfish integration", {"workflow_dispatch", "schedule"}),
    ):
        result = subprocess.run(
            ["gh", "api", "--method", "GET",
             f"repos/{repo}/actions/workflows/{workflow}/runs",
             "-f", f"head_sha={commit}", "-f", "branch=main",
             "-f", "status=success", "-f", "per_page=100"],
            check=True, capture_output=True, text=True,
        )
        runs = json.loads(result.stdout)["workflow_runs"]
        run = next((run for run in runs
                    if run["head_sha"] == commit
                    and run["head_branch"] == "main"
                    and run["event"] in events
                    and run["conclusion"] == "success"), None)
        if run is None:
            raise ValueError(
                f"{label} must succeed on main for exact commit {commit} before release."
            )
        evidence.append((label, run["html_url"]))
        print(f"{label}: {run['html_url']}")
    return evidence


def release_notes(version: str) -> str:
    changelog = Path("CHANGELOG.md").read_text(encoding="utf-8")
    heading = re.compile(r"^## \[" + re.escape(version) + r"\](?:[^\n]*)$", re.MULTILINE)
    match = heading.search(changelog)
    if match is None:
        raise ValueError(f"CHANGELOG.md has no entry for {version}")
    notes = re.split(r"^## ", changelog[match.end():], maxsplit=1, flags=re.MULTILINE)[0].strip()
    if not notes:
        raise ValueError(f"CHANGELOG.md entry for {version} is empty")
    return notes


def prepare(version: str, commit: str, repo: str, notes_file: Path) -> None:
    configured_version = ET.parse(PROJECT).getroot().findtext("./PropertyGroup/Version")
    if configured_version != version:
        raise ValueError(f"Requested version {version} does not match project version {configured_version}")
    notes = release_notes(version)
    evidence = require_evidence(repo, commit)
    lines = [
        f"## Stockfish.Uci {version}",
        "",
        notes,
        "",
        "### Installation",
        "",
        "```bash",
        f"dotnet add package {PACKAGE_ID} --version {version}",
        "```",
        "",
        "This package contains managed .NET code only. Install Stockfish separately.",
        "",
        "### Validation",
        "",
        *(f"- [{label}]({url})" for label, url in evidence),
        f"- Source commit: `{commit}`",
        "- Package contents checked and installed from a local feed before release.",
        "- SHA-256: see the attached `SHA256SUMS`.",
    ]
    notes_file.parent.mkdir(parents=True, exist_ok=True)
    notes_file.write_text("\n".join(lines) + "\n", encoding="utf-8")


def verify_package(directory: Path, version: str, commit: str, repo: str, seal: bool) -> Path:
    package = directory / f"{PACKAGE_ID}.{version}.nupkg"
    if list(directory.glob("*.nupkg")) != [package]:
        raise ValueError(f"Expected exactly one package: {package.name}")
    subprocess.run(
        [sys.executable, "scripts/verify-package.py", "--package", str(package),
         "--source-commit", commit],
        check=True,
    )
    with ZipFile(package) as archive:
        root = ET.fromstring(archive.read(f"{PACKAGE_ID}.nuspec"))
    metadata = root.find("./{*}metadata")
    if metadata is None:
        raise ValueError("Missing NuGet metadata")
    if metadata.findtext("./{*}id") != PACKAGE_ID or metadata.findtext("./{*}version") != version:
        raise ValueError("NuGet ID or version does not match the requested release")
    repository = metadata.find("./{*}repository")
    if repository is None or (
        repository.get("commit") != commit
        or repository.get("url") != f"https://github.com/{repo}"
        or repository.get("type") != "git"
    ):
        raise ValueError("Package source repository or commit does not match release tag")

    with package.open("rb") as source:
        digest = hashlib.file_digest(source, "sha256").hexdigest()
    manifest = directory / "SHA256SUMS"
    expected = f"{digest}  {package.name}\n"
    if seal:
        manifest.write_text(expected, encoding="utf-8")
    elif manifest.read_text(encoding="utf-8") != expected:
        raise ValueError("SHA256SUMS does not match the package")
    print(f"{package.name}: {digest}")
    return package


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    version_parser = commands.add_parser("version")
    version_parser.add_argument("--version", required=True)
    for command in ("prepare", "evidence", "seal", "verify"):
        sub = commands.add_parser(command)
        sub.add_argument("--commit", required=True)
        sub.add_argument("--repository", required=True)
        if command != "evidence":
            sub.add_argument("--version", required=True)
        if command == "prepare":
            sub.add_argument("--notes-file", required=True, type=Path)
        if command in ("seal", "verify"):
            sub.add_argument("--directory", required=True, type=Path)
    args = parser.parse_args()
    try:
        if hasattr(args, "version"):
            validate_version(args.version)
        if hasattr(args, "commit"):
            validate_commit(args.commit)
        if args.command == "prepare":
            prepare(args.version, args.commit, args.repository, args.notes_file)
        elif args.command == "evidence":
            require_evidence(args.repository, args.commit)
        elif args.command in ("seal", "verify"):
            verify_package(args.directory, args.version, args.commit, args.repository,
                           seal=args.command == "seal")
    except (ValueError, OSError, KeyError, ET.ParseError, BadZipFile, subprocess.CalledProcessError) as error:
        print(f"Release validation failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
