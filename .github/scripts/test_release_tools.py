"""Regression tests for release validation without network or publishing."""

import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
from unittest import TestCase, mock
from zipfile import ZipFile

SPEC = importlib.util.spec_from_file_location(
    "release_tools", Path(__file__).with_name("release-tools.py")
)
TOOLS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(TOOLS)


class ReleaseToolsTests(TestCase):
    COMMIT = "a" * 40
    REPOSITORY = "acidrod/Stockfish.Uci"

    def test_version_normalization(self):
        for version in ("0.1.0", "1.0.0", "2.0.0-rc.1"):
            self.assertEqual(TOOLS.validate_version(version), version)
        for version in ("v0.1.0", "01.1.0", "1.0", "1.0.0+build", "1.0.0-rc.01",
                        "../1.0.0", "1.0.0\n"):
            with self.subTest(version=version), self.assertRaises(ValueError):
                TOOLS.validate_version(version)

    def test_evidence_requires_the_exact_main_commit_and_events(self):
        def response(event, commit=self.COMMIT, branch="main"):
            return subprocess.CompletedProcess(
                [], 0, json.dumps({"workflow_runs": [{
                    "head_sha": commit, "head_branch": branch, "event": event,
                    "conclusion": "success", "html_url": "https://github.com/run/1",
                }]}), "",
            )

        with mock.patch.object(TOOLS.subprocess, "run",
                               side_effect=[response("push"), response("workflow_dispatch")]):
            self.assertEqual(len(TOOLS.require_evidence(self.REPOSITORY, self.COMMIT)), 2)
        for bad in (response("pull_request"), response("push", "b" * 40),
                    response("push", branch="feature/other")):
            with mock.patch.object(TOOLS.subprocess, "run", return_value=bad):
                with self.assertRaises(ValueError):
                    TOOLS.require_evidence(self.REPOSITORY, self.COMMIT)

    def test_seal_and_verify_reject_tampering(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            package = directory / "Stockfish.Uci.0.1.0.nupkg"
            with ZipFile(package, "w") as archive:
                archive.writestr("Stockfish.Uci.nuspec", f"""
                    <package><metadata><id>Stockfish.Uci</id>
                    <version>0.1.0</version>
                    <repository type="git" url="https://github.com/{self.REPOSITORY}"
                        commit="{self.COMMIT}" />
                    </metadata></package>""")
            # Managed content is checked separately by the existing package verifier.
            with mock.patch.object(TOOLS.subprocess, "run"):
                TOOLS.verify_package(directory, "0.1.0", self.COMMIT, self.REPOSITORY, True)
                TOOLS.verify_package(directory, "0.1.0", self.COMMIT, self.REPOSITORY, False)
                with self.assertRaises(ValueError):
                    TOOLS.verify_package(directory, "0.1.0", "b" * 40, self.REPOSITORY, False)
                with package.open("ab") as stream:
                    stream.write(b"tampered")
                with self.assertRaises(ValueError):
                    TOOLS.verify_package(directory, "0.1.0", self.COMMIT, self.REPOSITORY, False)


PACKAGE_SPEC = importlib.util.spec_from_file_location(
    "package_verifier", Path(__file__).resolve().parents[2] / "scripts" / "verify-package.py"
)
PACKAGE_VERIFIER = importlib.util.module_from_spec(PACKAGE_SPEC)
PACKAGE_SPEC.loader.exec_module(PACKAGE_VERIFIER)


class PackageIconTests(TestCase):
    ICON = "stockfish-uci-icon.png"
    NUSPEC = "Stockfish.Uci.nuspec"
    ICON_BYTES = (Path(__file__).resolve().parents[2] / "assets" / ICON).read_bytes()

    def verify_fixture(self, overrides=None, missing=()):
        entries = {
            "README.md": "Stockfish.Uci",
            "lib/net10.0/Stockfish.Uci.dll": b"managed assembly fixture",
            self.ICON: self.ICON_BYTES,
            self.NUSPEC: (
                '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
                f"<metadata><icon>{self.ICON}</icon></metadata></package>"
            ),
        }
        entries.update(overrides or {})
        for name in missing:
            del entries[name]
        with tempfile.TemporaryDirectory() as temp:
            package = Path(temp) / "Stockfish.Uci.0.1.0.nupkg"
            with ZipFile(package, "w") as archive:
                for name, content in entries.items():
                    archive.writestr(name, content)
            PACKAGE_VERIFIER.verify_package(package)

    def test_accepts_only_the_expected_package_icon(self):
        self.verify_fixture()

    def test_requires_the_icon_and_nuspec(self):
        for name in (self.ICON, self.NUSPEC):
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, "Missing required"):
                self.verify_fixture(missing=(name,))

    def test_rejects_unexpected_png_and_engine_assets(self):
        for name in ("another-icon.png", "assets/" + self.ICON, "Stockfish", "stockfish.exe",
                     "lib/net10.0/other.dll", "runtimes/linux-x64/native/stockfish"):
            with self.subTest(name=name), self.assertRaisesRegex(
                ValueError, "Unexpected package entry|External binary or archive|Runtime asset"
            ):
                self.verify_fixture(overrides={name: b"unexpected content"})

    def test_rejects_empty_invalid_oversized_and_wrong_dimension_icons(self):
        wrong_width = self.ICON_BYTES[:16] + (127).to_bytes(4, "big") + self.ICON_BYTES[20:]
        for content in (b"", b"not a PNG", self.ICON_BYTES[:33],
                        wrong_width, self.ICON_BYTES + b"x" * 1_000_000):
            with self.subTest(size=len(content)), self.assertRaisesRegex(
                ValueError, "empty|PNG|128|size"
            ):
                self.verify_fixture(overrides={self.ICON: content})

    def test_requires_exact_icon_metadata(self):
        for metadata in (
            "<metadata />",
            "<metadata><icon>other.png</icon></metadata>",
            "<metadata><icon>assets/stockfish-uci-icon.png</icon></metadata>",
            f"<metadata><icon>{self.ICON}</icon><icon>{self.ICON}</icon></metadata>",
            "<invalid",
        ):
            with self.subTest(metadata=metadata), self.assertRaisesRegex(ValueError, "icon|nuspec"):
                self.verify_fixture(overrides={self.NUSPEC: f"<package>{metadata}</package>"})
