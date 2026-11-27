#!/usr/bin/env python3
"""Verify the expected managed-only Stockfish.Uci NuGet package layout."""

import argparse
import re
import subprocess
from pathlib import Path, PurePosixPath
import xml.etree.ElementTree as ET
from zipfile import BadZipFile, ZipFile

MANAGED_ASSEMBLY = "lib/net10.0/Stockfish.Uci.dll"
PACKAGE_ICON = "stockfish-uci-icon.png"
NUSPEC = "Stockfish.Uci.nuspec"
ROOT = Path(__file__).resolve().parents[1]
MAX_RELEASE_NOTES_LENGTH = 35_000
MAX_ICON_SIZE = 1_000_000
REQUIRED = {"README.md", MANAGED_ASSEMBLY, PACKAGE_ICON, NUSPEC}
ALLOWED_FILES = REQUIRED | {
    "_rels/.rels",
    "[Content_Types].xml",
}
ALLOWED_DIRECTORIES = {
    "lib/",
    "lib/net10.0/",
    "_rels/",
    "package/",
    "package/services/",
    "package/services/metadata/",
    "package/services/metadata/core-properties/",
}
FORBIDDEN_SUFFIXES = {".exe", ".dll", ".so", ".dylib", ".zip", ".tar", ".gz", ".nnue"}


def is_core_properties(name: str) -> bool:
    path = PurePosixPath(name)
    return (
        len(path.parts) == 5
        and path.parts[:4] == ("package", "services", "metadata", "core-properties")
        and path.suffix == ".psmdcp"
        and str(path) == name
    )


def package_release_notes(changelog: str, version: str) -> str:
    """Independently derive the preformatted metadata expected from MSBuild."""
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9a-z.-]+)?", version):
        raise ValueError("Invalid or missing package version in nuspec")
    lines = changelog.splitlines()
    heading = f"## [{version}]"
    matches = [index for index, line in enumerate(lines) if line.startswith(heading)]
    if len(matches) != 1:
        raise ValueError(f"CHANGELOG.md must contain exactly one entry for {version}")

    section = []
    for line in lines[matches[0] + 1:]:
        if line.startswith("## "):
            break
        section.append(line)
    if not any(line.strip() and not line.lstrip().startswith("#") for line in section):
        raise ValueError(f"CHANGELOG.md entry for {version} is empty")

    notes = "\n".join(line[4:] if line.startswith("### ") else line for line in section).strip()
    result = (
        f"Stockfish.Uci {version}\n\n{notes}\n\n"
        f"Full release: https://github.com/acidrod/Stockfish.Uci/releases/tag/v{version}"
    )
    if len(result.encode("utf-16-le")) // 2 > MAX_RELEASE_NOTES_LENGTH:
        raise ValueError("Package release notes exceed NuGet.org's 35,000 character limit")
    return result


def verify_package_metadata(root: ET.Element, changelog: str) -> None:
    icons = root.findall("./{*}metadata/{*}icon")
    if len(icons) != 1 or icons[0].text != PACKAGE_ICON:
        raise ValueError(f"NuGet icon metadata must reference {PACKAGE_ICON}")

    versions = root.findall("./{*}metadata/{*}version")
    if len(versions) != 1 or not versions[0].text:
        raise ValueError("Missing or duplicate package version in nuspec")
    expected = package_release_notes(changelog, versions[0].text)
    notes = root.findall("./{*}metadata/{*}releaseNotes")
    if len(notes) != 1 or len(notes[0]) != 0 or notes[0].text != expected:
        raise ValueError("NuGet release notes must match the package version's CHANGELOG.md entry")


def verify_package_icon(archive: ZipFile) -> None:
    if archive.getinfo(PACKAGE_ICON).file_size > MAX_ICON_SIZE:
        raise ValueError(f"Package icon exceeds the maximum size: {PACKAGE_ICON}")

    # Check the PNG signature, IHDR chunk and dimensions without introducing
    # an image-library dependency into the package verification workflow.
    data = archive.read(PACKAGE_ICON)
    if (
        len(data) < 45
        or not data.startswith(b"\x89PNG\r\n\x1a\n\x00\x00\x00\x0dIHDR")
        or not data.endswith(b"\x00\x00\x00\x00IEND\xaeB`\x82")
    ):
        raise ValueError(f"Invalid PNG structure: {PACKAGE_ICON}")

    width = int.from_bytes(data[16:20], "big")
    height = int.from_bytes(data[20:24], "big")
    if (width, height) != (128, 128):
        raise ValueError(f"Package icon must be 128x128 pixels: {PACKAGE_ICON}")


def verify_package(package: Path, changelog: str | None = None) -> None:
    with ZipFile(package) as archive:
        seen: set[str] = set()

        for entry in archive.infolist():
            name = entry.filename
            normalized = name.lower()
            if normalized in seen:
                raise ValueError(f"Duplicate package entry: {name}")
            seen.add(normalized)

            if normalized == "runtimes" or normalized.startswith("runtimes/"):
                raise ValueError(f"Runtime asset is not allowed: {name}")

            if entry.is_dir():
                if name not in ALLOWED_DIRECTORIES or entry.file_size != 0:
                    raise ValueError(f"Unexpected package directory: {name}")
                continue

            if name != MANAGED_ASSEMBLY and PurePosixPath(normalized).suffix in FORBIDDEN_SUFFIXES:
                raise ValueError(f"External binary or archive is not allowed: {name}")

            # Fail closed: an extensionless engine executable or any other
            # unexpected content must not bypass the suffix checks.
            if name not in ALLOWED_FILES and not is_core_properties(name):
                raise ValueError(f"Unexpected package entry: {name}")

        missing = REQUIRED.difference(archive.namelist())
        if missing:
            raise ValueError(f"Missing required package entries: {', '.join(sorted(missing))}")

        for name in REQUIRED:
            if archive.getinfo(name).file_size == 0:
                raise ValueError(f"Required package entry is empty: {name}")

        verify_package_icon(archive)
        try:
            root = ET.fromstring(archive.read(NUSPEC))
        except ET.ParseError as error:
            raise ValueError(f"Invalid nuspec metadata: {NUSPEC}") from error
        if changelog is None:
            changelog = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
        verify_package_metadata(root, changelog)

        corrupt = archive.testzip()
        if corrupt is not None:
            raise ValueError(f"Corrupt package entry: {corrupt}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True, type=Path, help="Path to a .nupkg file")
    parser.add_argument("--source-commit", help="Read CHANGELOG.md from an exact Git source commit")
    args = parser.parse_args()

    try:
        changelog = None
        if args.source_commit:
            if not re.fullmatch(r"[0-9a-f]{40}", args.source_commit):
                raise ValueError("Expected a full 40-character Git commit SHA")
            changelog = subprocess.run(
                ["git", "show", f"{args.source_commit}:CHANGELOG.md"],
                cwd=ROOT, check=True, capture_output=True, encoding="utf-8",
            ).stdout
        verify_package(args.package, changelog)
    except (BadZipFile, OSError, RuntimeError, ValueError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"Package validation failed: {error}\n")

    print("Managed-only package validation passed.")


if __name__ == "__main__":
    main()
