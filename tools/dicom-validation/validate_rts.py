#!/usr/bin/env python3
"""Validate generated RT Structure Sets with dciodvfy (dicom3tools) and validate_iods (dicom-validator).

Usage:
    python -I tools/dicom-validation/validate_rts.py --rt-dir <folder> --out <folder>
        [--allowlist docs/evidence/validator-allowlist.json] [--edition 2025b] [--no-current]

What it does (docs/EVALUATION_PROTOCOL.md section 3 of the overhaul plan):
  1. Runs `dciodvfy -new` on every RT (every *.dcm whose Modality is RTSTRUCT); all of its output is on
     stderr. Lines starting with "Error - " are gated, "Warning - " lines are reported only.
  2. Runs `validate_iods --edition <edition>` over the same files. Its exit code is the error count. The
     edition is pinned to 2025b on purpose: it is the last edition in which Structure Set ROI, ROI Contour
     and RT ROI Observations sequences are Type 1, so an RT with zero ROIs fails. A second run against the
     current edition is report-only unless --no-current is given.
  3. Collapses sequence item indices ("[3]" -> "[n]") so one pattern covers every ROI, masks the values of
     patient-identifying attributes, and matches every error against the allowlist. An allowlist entry is
     {"tool": "dciodvfy"|"validate_iods", "pattern": <regex>, "id": "C9", "reason": ..., "added": ...}.
  4. Writes findings.json and summary.md (both safe to commit) and exits 1 when any error is not allowlisted
     or a tool could not be run.

The script never writes DICOM, needs no network, and does not print attribute values of group 0010.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from collections import Counter, OrderedDict
from pathlib import Path

PATIENT_TAG_RE = re.compile(r"\((0010,[0-9a-fA-F]{4})\)")
INDEX_RE = re.compile(r"\[\d+\]")
DCIODVFY_LINE_RE = re.compile(r"^(Error|Warning) - (.*)$")
VALIDATE_IODS_FILE_RE = re.compile(r"^Validating DICOM file (.+)$")
VALIDATE_IODS_FINDING_RE = re.compile(r"^\s{2}(.+)$")
VALIDATE_IODS_CONTEXT_RE = re.compile(r"^(Module \".*\":|\(\w{4},\w{4}\) \(.*\):)$")


def collapse(message: str) -> str:
    return INDEX_RE.sub("[n]", message)


def mask_patient_values(message: str) -> str:
    """Hide the value of any group 0010 attribute so reports can be committed to a public repository."""
    if PATIENT_TAG_RE.search(message):
        message = re.sub(r"= <[^>]*>", "= <MASKED>", message)
    return message


def modality(path: Path) -> str | None:
    try:
        import pydicom  # type: ignore

        ds = pydicom.dcmread(str(path), stop_before_pixels=True, specific_tags=["Modality"])
        return str(ds.get("Modality", "")) or None
    except Exception:  # pydicom missing or unreadable file: fall back to the file name
        return None


def rt_files(rt_dir: Path) -> list[Path]:
    files = sorted(p for p in rt_dir.rglob("*.dcm") if p.is_file())
    selected = []
    for path in files:
        mod = modality(path)
        if mod is None:
            if "_UID" in path.name:  # the generator's naming: <template>_UID<SeriesInstanceUID>.dcm
                selected.append(path)
        elif mod == "RTSTRUCT":
            selected.append(path)
    return selected


def package_version(name: str) -> str:
    try:
        from importlib.metadata import version  # type: ignore

        return version(name)
    except Exception as exc:  # noqa: BLE001
        return f"(unavailable: {exc})"


def run_dciodvfy(files: list[Path], out_dir: Path) -> tuple[list[dict], list[str]]:
    """Returns (findings, problems). Each finding: tool, severity, file, pattern, message, count."""
    out_dir.mkdir(parents=True, exist_ok=True)
    findings: list[dict] = []
    problems: list[str] = []
    exit_lines = []
    for path in files:
        try:
            result = subprocess.run(["dciodvfy", "-new", str(path)], capture_output=True, text=True, timeout=300)
        except FileNotFoundError:
            problems.append("dciodvfy is not installed (pip install dicom3tools)")
            return findings, problems
        except subprocess.TimeoutExpired:
            problems.append(f"dciodvfy timed out on {path.name}")
            continue
        report = mask_patient_values_in_text(result.stderr)
        (out_dir / (path.name + ".txt")).write_text(report, encoding="utf-8")
        exit_lines.append(f"{result.returncode} {path.name}")
        for line in result.stderr.splitlines():
            match = DCIODVFY_LINE_RE.match(line)
            if not match:
                continue
            severity, message = match.group(1), mask_patient_values(match.group(2))
            findings.append({
                "tool": "dciodvfy",
                "severity": severity.lower(),
                "file": path.name,
                "pattern": collapse(message),
                "message": message,
            })
    (out_dir / "exit.txt").write_text("\n".join(exit_lines) + "\n", encoding="utf-8")
    return findings, problems


def mask_patient_values_in_text(text: str) -> str:
    return "\n".join(mask_patient_values(line) for line in text.splitlines()) + ("\n" if text else "")


def run_validate_iods(files: list[Path], edition: str, out_file: Path) -> tuple[list[dict], int | None, list[str]]:
    """Returns (findings, exit code, problems). Findings are errors (validate_iods prints errors only)."""
    try:
        result = subprocess.run(
            ["validate_iods", "--edition", edition] + [str(p) for p in files],
            capture_output=True, text=True, timeout=1800,
        )
    except FileNotFoundError:
        return [], None, ["validate_iods is not installed (pip install dicom-validator)"]
    except subprocess.TimeoutExpired:
        return [], None, [f"validate_iods --edition {edition} timed out"]
    text = mask_patient_values_in_text(result.stdout + result.stderr)
    # Keep local folder names out of the committed reports: refer to files by name only.
    for path in files:
        text = text.replace(str(path), path.name).replace(str(path).replace(os.sep, "/"), path.name)
    out_file.write_text(text, encoding="utf-8")
    findings: list[dict] = []
    current_file = None
    context: list[str] = []
    for line in text.splitlines():
        file_match = VALIDATE_IODS_FILE_RE.match(line)
        if file_match:
            current_file = Path(file_match.group(1)).name
            context = []
            continue
        if VALIDATE_IODS_CONTEXT_RE.match(line):
            if line.startswith("Module"):
                context = [line]
            else:
                context = context[:1] + [line]
            continue
        finding = VALIDATE_IODS_FINDING_RE.match(line)
        if finding and current_file:
            message = " / ".join(context + [finding.group(1).strip()])
            findings.append({
                "tool": "validate_iods",
                "severity": "error",
                "file": current_file,
                "pattern": collapse(message),
                "message": message,
                "edition": edition,
            })
    return findings, result.returncode, []


def load_allowlist(path: Path | None) -> list[dict]:
    if path is None or not path.exists():
        return []
    data = json.loads(path.read_text(encoding="utf-8"))
    entries = data["entries"] if isinstance(data, dict) else data
    for entry in entries:
        entry["_re"] = re.compile(entry["pattern"])
    return entries


def match_allowlist(finding: dict, allowlist: list[dict]) -> dict | None:
    for entry in allowlist:
        if entry["tool"] == finding["tool"] and entry["_re"].search(finding["pattern"]):
            return entry
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--rt-dir", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--allowlist", type=Path, default=None)
    parser.add_argument("--edition", default="2025b", help="gated validate_iods edition (default 2025b)")
    parser.add_argument("--no-current", action="store_true", help="skip the report-only run against the current edition")
    args = parser.parse_args()

    files = rt_files(args.rt_dir)
    args.out.mkdir(parents=True, exist_ok=True)
    if not files:
        print(f"No RT Structure Set files under {args.rt_dir}", file=sys.stderr)
        return 1

    allowlist = load_allowlist(args.allowlist)
    versions = OrderedDict([
        ("dicom3tools", package_version("dicom3tools")),
        ("dicom-validator", package_version("dicom-validator")),
    ])

    dc_findings, problems = run_dciodvfy(files, args.out / "dciodvfy")
    vi_findings, vi_exit, vi_problems = run_validate_iods(files, args.edition, args.out / f"validate_iods-{args.edition}.txt")
    problems += vi_problems
    current_findings: list[dict] = []
    current_exit = None
    if not args.no_current:
        current_findings, current_exit, current_problems = run_validate_iods(files, "current", args.out / "validate_iods-current.txt")
        problems += current_problems

    gated = [f for f in dc_findings if f["severity"] == "error"] + vi_findings
    unallowed = []
    allowed_counter: Counter = Counter()
    for finding in gated:
        entry = match_allowlist(finding, allowlist)
        if entry is None:
            unallowed.append(finding)
        else:
            finding["allowlisted_as"] = entry["id"]
            allowed_counter[(finding["tool"], entry["id"], finding["pattern"])] += 1

    warnings_counter = Counter((f["tool"], f["pattern"]) for f in dc_findings if f["severity"] == "warning")
    current_counter = Counter(f["pattern"] for f in current_findings)

    summary = {
        "files": len(files),
        "tools": dict(versions),
        "gatedEdition": args.edition,
        "dciodvfy": {"errors": sum(1 for f in dc_findings if f["severity"] == "error"), "warnings": len(dc_findings) - sum(1 for f in dc_findings if f["severity"] == "error")},
        "validate_iods": {"edition": args.edition, "errors": len(vi_findings), "exitCode": vi_exit},
        "validate_iods_current": {"errors": len(current_findings), "exitCode": current_exit} if not args.no_current else None,
        "allowlisted": [{"tool": t, "id": i, "pattern": p, "count": c} for (t, i, p), c in sorted(allowed_counter.items())],
        "unallowed": [{k: v for k, v in f.items() if k != "_re"} for f in unallowed],
        "warnings": [{"tool": t, "pattern": p, "count": c} for (t, p), c in sorted(warnings_counter.items())],
        "currentEditionErrors": [{"pattern": p, "count": c} for p, c in sorted(current_counter.items())],
        "problems": problems,
        "passed": not unallowed and not problems,
    }
    (args.out / "findings.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    lines = [
        "# DICOM validation report",
        "",
        f"Files: {len(files)} RT Structure Set(s). Tools: " + "; ".join(f"{k} {v}" for k, v in summary["tools"].items()) + ".",
        f"Gate: `dciodvfy` errors and `validate_iods --edition {args.edition}` errors must be allowlisted. Result: **{'PASS' if summary['passed'] else 'FAIL'}**.",
        "",
        f"- dciodvfy: {summary['dciodvfy']['errors']} error(s), {summary['dciodvfy']['warnings']} warning(s)",
        f"- validate_iods {args.edition}: {len(vi_findings)} error(s), exit code {vi_exit}",
    ]
    if not args.no_current:
        lines.append(f"- validate_iods current (report only): {len(current_findings)} error(s), exit code {current_exit}")
    if problems:
        lines += ["", "## Problems", ""] + [f"- {p}" for p in problems]
    lines += ["", "## Errors not in the allowlist", ""]
    lines += [f"- `{f['tool']}` {f['file']}: {f['message']}" for f in unallowed] or ["none"]
    lines += ["", "## Allowlisted errors", ""]
    lines += [f"- `{a['tool']}` {a['id']} x{a['count']}: {a['pattern']}" for a in summary["allowlisted"]] or ["none"]
    lines += ["", "## Warnings (not gated)", ""]
    lines += [f"- `{w['tool']}` x{w['count']}: {w['pattern']}" for w in summary["warnings"]] or ["none"]
    if not args.no_current:
        lines += ["", "## Current-edition errors (report only)", ""]
        lines += [f"- x{c['count']}: {c['pattern']}" for c in summary["currentEditionErrors"]] or ["none"]
    (args.out / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    print("\n".join(lines))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
