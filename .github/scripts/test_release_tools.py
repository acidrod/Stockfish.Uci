"""Regression tests for release validation without network or publishing."""

import importlib.util
import json
from pathlib import Path
import subprocess
import shutil
import tempfile
from unittest import TestCase, mock, skipUnless
from zipfile import ZipFile
from xml.sax.saxutils import escape

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
            with mock.patch.object(TOOLS.subprocess, "run") as run:
                TOOLS.verify_package(directory, "0.1.0", self.COMMIT, self.REPOSITORY, True)
                self.assertEqual(run.call_args.args[0][-2:], ["--source-commit", self.COMMIT])
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


ROOT = Path(__file__).resolve().parents[2]
FIXTURE_CHANGELOG = """# Changelog

## [Unreleased]

- Unreleased work.

## [0.1.10]

- A different version.

## [0.1.1] - 2026-11-27

### Added

- Icon & release notes, 100% ready; <safe>.

### Compatibility

- No breaking changes.

## [0.1.0]

- Initial release.
"""
EXPECTED_NOTES = """Stockfish.Uci 0.1.1

Added

- Icon & release notes, 100% ready; <safe>.

Compatibility

- No breaking changes.

Full release: https://github.com/acidrod/Stockfish.Uci/releases/tag/v0.1.1"""


class PackageIconTests(TestCase):
    ICON = "stockfish-uci-icon.png"
    NUSPEC = "Stockfish.Uci.nuspec"
    ICON_BYTES = (Path(__file__).resolve().parents[2] / "assets" / ICON).read_bytes()

    def verify_fixture(self, overrides=None, missing=(), changelog=FIXTURE_CHANGELOG):
        entries = {
            "README.md": "Stockfish.Uci",
            "lib/net10.0/Stockfish.Uci.dll": b"managed assembly fixture",
            self.ICON: self.ICON_BYTES,
            self.NUSPEC: (
                '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
                f"<metadata><icon>{self.ICON}</icon><version>0.1.1</version>"
                f"<releaseNotes>{escape(EXPECTED_NOTES)}</releaseNotes></metadata></package>"
            ),
        }
        entries.update(overrides or {})
        for name in missing:
            del entries[name]
        with tempfile.TemporaryDirectory() as temp:
            package = Path(temp) / "Stockfish.Uci.0.1.1.nupkg"
            with ZipFile(package, "w") as archive:
                for name, content in entries.items():
                    archive.writestr(name, content)
            PACKAGE_VERIFIER.verify_package(package, changelog)

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


    def test_requires_exact_version_specific_release_notes(self):
        for notes in ("", "<releaseNotes />",
                      "<releaseNotes>https://github.com/acidrod/Stockfish.Uci/releases</releaseNotes>",
                      "<releaseNotes>Wrong version notes</releaseNotes>",
                      f"<releaseNotes>{escape(EXPECTED_NOTES)}</releaseNotes>" * 2,
                      f"<releaseNotes>{escape(EXPECTED_NOTES)}<extra /></releaseNotes>"):
            with self.subTest(notes=notes), self.assertRaisesRegex(ValueError, "release notes"):
                self.verify_fixture(overrides={self.NUSPEC: (
                    f"<package><metadata><icon>{self.ICON}</icon><version>0.1.1</version>"
                    f"{notes}</metadata></package>"
                )})

    def test_requires_exact_package_version(self):
        for versions in ("", "<version />", "<version>0.1.0</version>",
                         "<version>0.1.1</version><version>0.1.1</version>"):
            with self.subTest(versions=versions), self.assertRaisesRegex(ValueError, "version|release notes"):
                self.verify_fixture(overrides={self.NUSPEC: (
                    f"<package><metadata><icon>{self.ICON}</icon>{versions}"
                    f"<releaseNotes>{escape(EXPECTED_NOTES)}</releaseNotes></metadata></package>"
                )})


class PackageReleaseNotesTests(TestCase):
    def test_exact_section_excludes_unreleased_and_other_versions(self):
        for text in (FIXTURE_CHANGELOG, FIXTURE_CHANGELOG.replace("\n", "\r\n")):
            self.assertEqual(PACKAGE_VERIFIER.package_release_notes(text, "0.1.1"), EXPECTED_NOTES)

    def test_prerelease_and_last_section(self):
        notes = PACKAGE_VERIFIER.package_release_notes(
            "## [0.2.0]\n- Stable.\n## [0.2.0-rc.1]\n### Added\n- Preview.", "0.2.0-rc.1"
        )
        self.assertEqual(notes, "Stockfish.Uci 0.2.0-rc.1\n\nAdded\n- Preview.\n\n"
                                "Full release: https://github.com/acidrod/Stockfish.Uci/releases/tag/v0.2.0-rc.1")

    def test_missing_duplicate_and_empty_sections_are_rejected(self):
        for text in ("## [0.1.10]\n- Not this version.",
                     "## [0.1.1]\n- One.\n## [0.1.1]\n- Two.",
                     "## [0.1.1]\n\n## [0.1.0]\n- Old.",
                     "## [0.1.1]\n### Added\n"):
            with self.subTest(text=text), self.assertRaisesRegex(ValueError, "exactly one|empty"):
                PACKAGE_VERIFIER.package_release_notes(text, "0.1.1")

    def test_rejects_oversized_release_notes(self):
        with self.assertRaisesRegex(ValueError, "35,000"):
            PACKAGE_VERIFIER.package_release_notes("## [0.1.1]\n" + "x" * 35000, "0.1.1")


@skipUnless(shutil.which("dotnet"), "The .NET SDK is required to exercise the MSBuild task")
class MSBuildReleaseNotesTests(TestCase):
    def run_target(self, changelog, version="0.1.1"):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "CHANGELOG.md"
            path.write_bytes(changelog.encode("utf-8"))
            return subprocess.run(
                ["dotnet", "msbuild", str(ROOT / TOOLS.PROJECT), "-nologo",
                 "-t:StockfishGeneratePackageReleaseNotes",
                 f"-p:StockfishChangelogPath={path}", f"-p:PackageVersion={version}",
                 "-getProperty:PackageReleaseNotes"],
                cwd=ROOT, capture_output=True, encoding="utf-8", timeout=90,
            )

    def test_msbuild_output_matches_independent_expectation(self):
        for text in (FIXTURE_CHANGELOG, FIXTURE_CHANGELOG.replace("\n", "\r\n")):
            with self.subTest(line_endings=repr(text[:20])):
                result = self.run_target(text)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertEqual(result.stdout.strip(), EXPECTED_NOTES)

    def test_msbuild_fails_for_missing_duplicate_or_empty_sections(self):
        for text in ("## [0.1.10]\n- Not this version.",
                     "## [0.1.1]\n- One.\n## [0.1.1]\n- Two.",
                     "## [0.1.1]\n### Added\n"):
            with self.subTest(text=text):
                result = self.run_target(text)
                self.assertNotEqual(result.returncode, 0)
                self.assertRegex(result.stdout + result.stderr, r"exactly one|is empty")

    def test_msbuild_rejects_oversized_release_notes(self):
        result = self.run_target("## [0.1.1]\n" + "x" * 35000)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("35,000", result.stdout + result.stderr)
