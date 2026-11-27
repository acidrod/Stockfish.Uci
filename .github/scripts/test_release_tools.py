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
