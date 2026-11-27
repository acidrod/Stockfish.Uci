#!/usr/bin/env python3
"""Write a VSTest TRX report to the GitHub Actions job summary."""

import argparse
from collections import Counter
from datetime import datetime
from html import escape
import os
from pathlib import Path
import xml.etree.ElementTree as ET


SUITES = {
    "unit": "Unit tests",
    "integration": "Stockfish 18 integration tests",
}


def safe(value: str) -> str:
    # Escape HTML and Markdown table delimiters in test display names.
    return escape(" ".join(value.split())).replace("|", "&#124;")


def code(value: str) -> str:
    return f"<code>{safe(value)}</code>"


def format_duration(value: str) -> str:
    hours, minutes, seconds = value.split(":")
    total = int(hours) * 3600 + int(minutes) * 60 + float(seconds)
    return f"{total * 1000:.1f} ms" if total < 1 else f"{total:.2f} s"


def outcome_label(outcome: str) -> str:
    if outcome == "Passed":
        return "✅ Passed"
    if outcome in ("NotExecuted", "Skipped"):
        return "⏭️ Skipped"
    return f"❌ {safe(outcome)}"


def render_report(
    trx: Path, suite: str, outcome: str, job_status: str
) -> tuple[str, bool]:
    platform = safe(os.environ.get("RUNNER_OS", "Local"))
    architecture = safe(os.environ.get("RUNNER_ARCH", "Unknown"))
    lines = [
        f"## {SUITES[suite]} · {platform} ({architecture})",
        "",
        f"**Workflow job status before reporting:** {safe(job_status)}",
        "",
    ]

    if not trx.is_file():
        lines += [
            f"⚠️ **No test results available.** Test step: {safe(outcome or 'unknown')}.",
            "",
            "See the workflow logs for the step that prevented tests from completing.",
        ]
        return "\n".join(lines), False

    root = ET.parse(trx).getroot()
    results = root.findall("./{*}Results/{*}UnitTestResult")
    counters = root.find("./{*}ResultSummary/{*}Counters")
    times = root.find("./{*}Times")
    if counters is None or times is None:
        raise ValueError("Missing TRX run counters or timing information")

    total = int(counters.attrib["total"])
    if len(results) != total:
        raise ValueError("TRX result count does not match run counters")

    # VSTest may report zero notExecuted in Counters even when individual
    # xUnit results have outcome=NotExecuted. Count the actual result rows.
    outcomes = Counter(result.attrib["outcome"] for result in results)
    passed = outcomes["Passed"]
    failed = outcomes["Failed"]
    skipped = outcomes["NotExecuted"] + outcomes["Skipped"]
    other = total - passed - failed - skipped

    duration = datetime.fromisoformat(times.attrib["finish"]) - datetime.fromisoformat(times.attrib["start"])
    seconds = duration.total_seconds()
    if seconds < 0:
        raise ValueError("Invalid TRX run duration")

    successful = (
        outcome == "success"
        and total > 0
        and passed > 0
        and failed == 0
        and other == 0
        and (suite != "integration" or skipped == 0)
    )
    completed = passed + failed + other
    rate = f"{passed / completed:.1%}" if completed else "N/A"
    if successful:
        status = "✅ **Passed**" if skipped == 0 else "⚠️ **Passed with skipped tests**"
    else:
        status = "❌ **Failed or incomplete**"

    lines += [
        status,
        "",
        "| 📋 Total | ✅ Passed | ❌ Failed | ⏭️ Skipped | ⚠️ Other |",
        "| ---: | ---: | ---: | ---: | ---: |",
        f"| **{total}** | **{passed}** | **{failed}** | **{skipped}** | **{other}** |",
        "",
        f"**Pass rate (non-skipped tests):** {rate} · **Run duration:** {seconds:.2f} s",
        "",
    ]

    failed_results = [
        result for result in results
        if result.attrib["outcome"] not in ("Passed", "NotExecuted", "Skipped")
    ]
    if failed_results:
        lines += ["### Failed or incomplete tests", ""]
        for result in sorted(failed_results, key=lambda item: item.attrib["testName"]):
            lines.append(
                f"- {code(result.attrib['testName'])} — {outcome_label(result.attrib['outcome'])}"
            )
        lines += ["", "See the test step logs for error messages and stack traces.", ""]

    lines += [
        "<details>",
        f"<summary>Individual test results ({total})</summary>",
        "",
        "| Test | Result | Duration |",
        "| --- | --- | ---: |",
    ]
    for result in sorted(results, key=lambda item: item.attrib["testName"]):
        name = result.attrib["testName"].removeprefix("Stockfish.Uci.")
        lines.append(
            f"| {code(name)} | {outcome_label(result.attrib['outcome'])} | "
            f"{format_duration(result.attrib['duration']) if 'duration' in result.attrib else '—'} |"
        )
    lines += ["", "</details>", ""]
    if suite == "integration" and skipped:
        lines += ["⚠️ All integration tests must run against the configured engine.", ""]
    return "\n".join(lines), successful


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--suite", required=True, choices=SUITES)
    parser.add_argument("--trx", required=True, type=Path)
    parser.add_argument("--outcome", required=True)
    parser.add_argument("--job-status", required=True)
    args = parser.parse_args()

    try:
        report, successful = render_report(args.trx, args.suite, args.outcome, args.job_status)
    except (ET.ParseError, OSError, ValueError, KeyError) as error:
        report = f"## {SUITES[args.suite]}\n\n❌ **Could not read test results.**\n\n{safe(str(error))}"
        successful = False

    with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary:
        summary.write(report + "\n")

    # A test failure already fails its step. A report must never turn it green,
    # and missing/empty results must not pass a successful test command.
    return 1 if not successful and (args.outcome == "success" or args.job_status == "success") else 0


if __name__ == "__main__":
    raise SystemExit(main())
