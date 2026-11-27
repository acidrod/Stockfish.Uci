#!/usr/bin/env python3
"""Prepare a pinned official Stockfish release for CI integration tests only."""

import hashlib
import os
from pathlib import Path, PurePosixPath
import shutil
import stat
import subprocess
import tarfile
import tempfile
from urllib.request import urlopen
import zipfile

TAG = "sf_18"
BASE_URL = f"https://github.com/official-stockfish/Stockfish/releases/download/{TAG}"
# Asset names and digests from the official GitHub release (tag sf_18).
ASSETS = {
    ("Linux", "X64"): (
        "stockfish-ubuntu-x86-64.tar",
        "5c6f38b02a4da5f3ffe763f27da6c3e743eebefd92b50cb3661623b96696adff",
    ),
    ("Windows", "X64"): (
        "stockfish-windows-x86-64.zip",
        "40cc975817e7eee270b03f354810d20956df565420d320f6dd37d454dc81a139",
    ),
    ("macOS", "X64"): (
        "stockfish-macos-x86-64.tar",
        "e7d7a2bca13915419d41ac6cb8cedb123dd2ba1c39a22c574df7a2aa3f526592",
    ),
    ("macOS", "ARM64"): (
        "stockfish-macos-m1-apple-silicon.tar",
        "4d77c4aa3ad9bd1ea8111f2ac5a4620fe7ebf998d6893bf828d49ccd579c8cb0",
    ),
}


def extract_executable(archive: Path, executable: Path) -> None:
    # Copy only the named regular executable, never extract arbitrary paths.
    if archive.suffix == ".zip":
        with zipfile.ZipFile(archive) as source:
            entries = [
                entry for entry in source.infolist()
                if PurePosixPath(entry.filename).name == executable.name
                and not entry.is_dir()
                and not stat.S_ISLNK(entry.external_attr >> 16)
            ]
            if len(entries) != 1:
                raise ValueError(f"Expected exactly one {executable.name} in {archive.name}")
            with source.open(entries[0]) as data, executable.open("xb") as target:
                shutil.copyfileobj(data, target)
    else:
        with tarfile.open(archive) as source:
            entries = [
                entry for entry in source.getmembers()
                if PurePosixPath(entry.name).name == executable.name and entry.isfile()
            ]
            if len(entries) != 1:
                raise ValueError(f"Expected exactly one {executable.name} in {archive.name}")
            data = source.extractfile(entries[0])
            if data is None:
                raise ValueError(f"Cannot read {entries[0].name}")
            with data, executable.open("xb") as target:
                shutil.copyfileobj(data, target)


def main() -> None:
    platform = (os.environ["RUNNER_OS"], os.environ["RUNNER_ARCH"])
    if platform not in ASSETS:
        raise ValueError(f"No pinned Stockfish asset for {platform}")
    asset, expected_digest = ASSETS[platform]
    url = f"{BASE_URL}/{asset}"
    print(f"Upstream Stockfish tag: {TAG}", flush=True)
    print(f"Upstream Stockfish URL: {url}", flush=True)
    print(f"Expected SHA-256: {expected_digest}", flush=True)

    directory = Path(tempfile.mkdtemp(
        prefix="stockfish-integration-", dir=os.environ["RUNNER_TEMP"]
    ))
    archive = directory / asset
    executable = directory / (archive.stem + (".exe" if platform[0] == "Windows" else ""))

    with urlopen(url, timeout=120) as response, archive.open("xb") as target:
        shutil.copyfileobj(response, target)
    with archive.open("rb") as source:
        actual_digest = hashlib.file_digest(source, "sha256").hexdigest()
    if actual_digest != expected_digest:
        raise ValueError(f"SHA-256 mismatch: {actual_digest}")

    extract_executable(archive, executable)
    archive.unlink()
    if platform[0] != "Windows":
        executable.chmod(0o700)

    handshake = subprocess.run(
        [str(executable)], input="uci\nquit\n", text=True,
        capture_output=True, timeout=30, check=True,
    )
    if "id name Stockfish 18" not in handshake.stdout.splitlines():
        raise ValueError(f"Unexpected engine identity: {handshake.stdout}")
    print("Verified engine: Stockfish 18")

    # Only subsequent steps on this ephemeral runner receive the executable path.
    with open(os.environ["GITHUB_ENV"], "a", encoding="utf-8") as environment:
        environment.write(f"STOCKFISH_UCI_PATH={executable}\n")


if __name__ == "__main__":
    main()
