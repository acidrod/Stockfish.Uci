#!/usr/bin/env python3
"""Verify the expected managed-only Stockfish.Uci NuGet package layout."""

import argparse
from pathlib import Path, PurePosixPath
from zipfile import BadZipFile, ZipFile

MANAGED_ASSEMBLY = "lib/net10.0/Stockfish.Uci.dll"
REQUIRED = {"README.md", MANAGED_ASSEMBLY}
ALLOWED_FILES = REQUIRED | {
    "Stockfish.Uci.nuspec",
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


def verify_package(package: Path) -> None:
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

        corrupt = archive.testzip()
        if corrupt is not None:
            raise ValueError(f"Corrupt package entry: {corrupt}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True, type=Path, help="Path to a .nupkg file")
    args = parser.parse_args()

    try:
        verify_package(args.package)
    except (BadZipFile, OSError, RuntimeError, ValueError) as error:
        parser.exit(1, f"Package validation failed: {error}\n")

    print("Managed-only package validation passed.")


if __name__ == "__main__":
    main()
