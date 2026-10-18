#!/usr/bin/env python3
"""Check the published TG-263 structure list against the AAPM TG-263 worksheet and the TG-263 naming rules.

Usage:
    python -I -m pip install "xlrd==2.0.1"
    python -I tools/nomenclature/check_tg263_names.py --snapshot <TG263 snapshot .json> --out <folder>
        [--worksheet <local TG263_Nomenclature_Worksheet_20170815.xls>]

Overhaul plan step P1.8. Example (the baseline evidence):
    python -I tools/nomenclature/check_tg263_names.py \
        --snapshot TemplateSync.Tests/TestData/TG263.2026-09-30.json \
        --out docs/evidence/baseline-e7f0572/nomenclature

The worksheet is fetched, not stored. AAPM publishes it on the "Radiation Oncology Nomenclature Resource Page"
(WORKSHEET_URL below) without a stated licence, so it must never be committed to this public repository. Without
--worksheet the script downloads it into a fresh temporary directory, records its URL, size, SHA-256 and
Last-Modified header in the report, reads it, and deletes the directory. With --worksheet no download happens and
the given file's size and SHA-256 are recorded instead. The worksheet's contents are treated as data only. Because
it needs the network, this check is run locally and is not part of CI.

What it does:
  1. Finds the primary-name, reverse-order-name, target-type, category and FMAID columns of the worksheet by
     header text and reports which headers matched.
  2. For every snapshot record, matches `Structure` and `TG_263` against the worksheet primary names and
     `TG_263R` against the reverse-order names: exactly, ignoring case only, or not at all. For matched names it
     reports the worksheet FMAID (the snapshot has no FMAID column, so this is availability, not agreement).
  3. Applies the in-repo TG-263 rule checks (RULES below) to `Structure` and lists each violation with the
     record id and name. The same rules are also run over the worksheet's own primary names, as a calibration of
     the rules (a rule that flags many worksheet names is stricter than TG-263 itself).
  4. Writes nomenclature.json and nomenclature.md (both safe to commit: they hold only names from the snapshot,
     the matched worksheet spellings and FMAIDs, and the worksheet's provenance).

Dependencies: xlrd (pinned above; the worksheet is a legacy .xls) and the standard library.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
import tempfile
import urllib.request
from collections import Counter, OrderedDict, defaultdict
from datetime import datetime, timezone
from pathlib import Path

WORKSHEET_PAGE = "https://www.aapm.org/pubs/reports/RPT_263_Supplemental/"
WORKSHEET_URL = WORKSHEET_PAGE + "TG263_Nomenclature_Worksheet_20170815.xls"

# Column roles, each found by a case-insensitive substring of the header text (first matching column wins).
COLUMN_ROLES = OrderedDict(
    [
        ("primary", ("primary name", "primary")),
        ("reverse", ("reverse order name", "reverse")),
        ("target_type", ("target type",)),
        ("category", ("major category", "category")),
        ("fmaid", ("fmaid", "fma id", "fma")),
    ]
)

RULE_LABELS = {
    "length": "At most 16 characters",
    "unique": "Unique ignoring case",
    "target_prefix": "Targets start with GTV/CTV/ITV/PTV",
    "helper_prefix": "Helpers start with `z` or `_`",
    "laterality": "Laterality only as trailing `_L`/`_R`",
    "no_spaces": "No spaces",
    "charset": "Only letters, digits, `_ ^ + - . /`",
}

MAX_LENGTH = 16
TARGET_TYPES = {"GTV", "CTV", "ITV", "PTV"}
TARGET_PREFIXES = ("GTV", "CTV", "ITV", "PTV")
HELPER_PREFIXES = ("z", "_")
HELPER_TYPES = {"CONTROL"}
# A name token that marks an optimisation or other non-evaluation helper. The negative look-ahead keeps
# anatomy such as "OpticNrv" or "Ringer" out.
HELPER_WORD_RE = re.compile(r"(?i)(opt|ring|avoid|ctrl|control|dummy|temp|help|helper)(?![a-z])")
LATERALITY_WORDS = {"l", "r", "lt", "rt", "left", "right"}
ALLOWED_CHAR_RE = re.compile(r"[A-Za-z0-9_^+\-./]")

RULES = OrderedDict(
    [
        ("length", f"At most {MAX_LENGTH} characters (Unicode code points)."),
        ("unique", "Unique ignoring case across the list (each record of a colliding group is listed)."),
        (
            "target_prefix",
            "Target names start with GTV, CTV, ITV or PTV. A record is a target when its snapshot Type is GTV, CTV, "
            "ITV or PTV. A target whose name starts with a helper prefix (`z` or `_`) is a helper, not checked here, "
            "and listed separately.",
        ),
        (
            "helper_prefix",
            "Helper or non-evaluation structures start with `z` or `_`. A record is a helper when its snapshot Type "
            "is CONTROL (any case) or its name has a helper word (opt, ring, avoid, ctrl, control, dummy, temp, help, "
            "helper) not followed by a letter; this is a heuristic, so the helpers found are listed.",
        ),
        (
            "laterality",
            "Laterality only as a trailing `_L` or `_R`: no `L`/`R` token before the last one, and no `Lt`, `Rt`, "
            "`Left`, `Right` or lower-case `l`/`r` token anywhere (tokens are the parts between `_`).",
        ),
        ("no_spaces", "No spaces (or other whitespace)."),
        ("charset", "Only letters A-Z/a-z, digits, `_`, `^`, `+`, `-`, `.` and `/`."),
    ]
)


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def download_worksheet(directory: Path) -> tuple[Path, dict]:
    target = directory / WORKSHEET_URL.rsplit("/", 1)[1]
    request = urllib.request.Request(WORKSHEET_URL, headers={"User-Agent": "check_tg263_names.py"})
    with urllib.request.urlopen(request, timeout=60) as response, target.open("wb") as out:
        shutil.copyfileobj(response, out)
        headers = {"lastModified": response.headers.get("Last-Modified"), "contentType": response.headers.get("Content-Type")}
    return target, headers


def cell_text(value) -> str:
    if isinstance(value, float):
        return str(int(value)) if value.is_integer() else repr(value)
    return str(value).strip()


def read_worksheet(path: Path) -> dict:
    import xlrd  # type: ignore

    book = xlrd.open_workbook(str(path))
    sheet = book.sheet_by_index(0)
    headers = [cell_text(v) for v in sheet.row_values(0)]
    columns = OrderedDict()
    for role, needles in COLUMN_ROLES.items():
        found = None
        for needle in needles:
            for index, header in enumerate(headers):
                if needle in header.lower() and index not in [c["index"] for c in columns.values() if c]:
                    found = {"index": index, "header": header, "matchedOn": needle}
                    break
            if found:
                break
        columns[role] = found
    missing = [role for role in ("primary", "reverse") if not columns[role]]
    if missing:
        raise SystemExit(f"Worksheet columns not found: {', '.join(missing)} (headers: {headers})")

    rows = []
    for r in range(1, sheet.nrows):
        values = sheet.row_values(r)
        row = {role: (cell_text(values[c["index"]]) if c else "") for role, c in columns.items()}
        row["row"] = r + 1  # 1-based spreadsheet row
        if row["primary"] or row["reverse"]:
            rows.append(row)
    return {
        "xlrdVersion": xlrd.__VERSION__,
        "sheetNames": book.sheet_names(),
        "sheet": sheet.name,
        "rowCount": sheet.nrows,
        "dataRowCount": len(rows),
        "headers": headers,
        "columns": columns,
        "rows": rows,
    }


class NameIndex:
    def __init__(self, rows: list[dict], key: str):
        self.exact = {}
        self.folded = defaultdict(list)
        for row in rows:
            name = row[key]
            if not name:
                continue
            self.exact.setdefault(name, row)
            self.folded[name.casefold()].append(row)
        self.key = key

    def match(self, name: str | None) -> dict:
        if name is None or name == "":
            return {"status": "missing", "worksheetName": None, "worksheetRow": None, "fmaid": None}
        row = self.exact.get(name)
        status = "exact"
        if row is None:
            candidates = self.folded.get(name.casefold(), [])
            row = candidates[0] if candidates else None
            status = "case" if row else "none"
        if row is None:
            return {"status": "none", "worksheetName": None, "worksheetRow": None, "fmaid": None}
        return {
            "status": status,
            "worksheetName": row[self.key],
            "worksheetRow": row["row"],
            "fmaid": row.get("fmaid") or None,
            "targetType": row.get("target_type") or None,
            "category": row.get("category") or None,
        }


def tokens(name: str) -> list[str]:
    return name.split("_")


def is_target(record_type: str | None) -> bool:
    return (record_type or "").strip().upper() in TARGET_TYPES


def is_helper(name: str, record_type: str | None) -> bool:
    return (record_type or "").strip().upper() in HELPER_TYPES or bool(HELPER_WORD_RE.search(name))


def rule_findings(name: str, record_type: str | None) -> dict[str, str]:
    """Return {rule: detail} for every per-name rule this name breaks (uniqueness is checked over the list)."""
    found = {}
    if len(name) > MAX_LENGTH:
        found["length"] = f"{len(name)} characters"
    if is_target(record_type) and not name.startswith(HELPER_PREFIXES) and not name.upper().startswith(TARGET_PREFIXES):
        found["target_prefix"] = f"Type {record_type}"
    if is_helper(name, record_type) and not name.startswith(HELPER_PREFIXES):
        found["helper_prefix"] = f"Type {record_type}" if (record_type or "").strip().upper() in HELPER_TYPES else (
            "helper word " + HELPER_WORD_RE.search(name).group(0))
    parts = tokens(name)
    bad_lat = []
    for i, part in enumerate(parts):
        last = i == len(parts) - 1
        if part in ("L", "R"):
            if not last:
                bad_lat.append(f"`{part}` not last")
        elif part.lower() in LATERALITY_WORDS:
            bad_lat.append(f"`{part}`")
    if bad_lat:
        found["laterality"] = ", ".join(bad_lat)
    if any(ch.isspace() for ch in name):
        found["no_spaces"] = f"{sum(ch.isspace() for ch in name)} whitespace character(s)"
    bad = sorted({ch for ch in name if not ALLOWED_CHAR_RE.fullmatch(ch) and not ch.isspace()})
    if bad:
        found["charset"] = ", ".join(f"U+{ord(ch):04X} {ch!r}" for ch in bad)
    return found


def check(snapshot: dict, worksheet: dict) -> dict:
    primary = NameIndex(worksheet["rows"], "primary")
    reverse = NameIndex(worksheet["rows"], "reverse")
    records = []
    violations = OrderedDict((rule, []) for rule in RULES)
    by_fold = defaultdict(list)
    helpers = []
    prefixed_targets = []
    for rec in snapshot["records"]:
        fields = rec.get("fields", {})
        name = fields.get("Structure") or ""
        rtype = fields.get("Type")
        m_struct = primary.match(fields.get("Structure"))
        m_tg = primary.match(fields.get("TG_263"))
        m_rev = reverse.match(fields.get("TG_263R"))
        fmaid = m_struct["fmaid"] or m_tg["fmaid"]
        findings = rule_findings(name, rtype)
        for rule, detail in findings.items():
            violations[rule].append({"id": rec.get("id"), "structure": name, "detail": detail})
        if is_helper(name, rtype):
            helpers.append({"id": rec.get("id"), "structure": name, "type": rtype})
        if is_target(rtype) and name.startswith(HELPER_PREFIXES):
            prefixed_targets.append({"id": rec.get("id"), "structure": name, "type": rtype})
        by_fold[name.casefold()].append(rec)
        records.append(
            OrderedDict(
                [
                    ("id", rec.get("id")),
                    ("structure", name),
                    ("tg263", fields.get("TG_263")),
                    ("tg263r", fields.get("TG_263R")),
                    ("type", rtype),
                    ("templateRecommend", fields.get("Template_Recommend") or []),
                    ("templateConsider", fields.get("Template_Consider") or []),
                    ("structureMatch", m_struct),
                    ("tg263Match", m_tg),
                    ("tg263rMatch", m_rev),
                    ("worksheetFmaid", fmaid),
                    ("ruleViolations", sorted(findings)),
                ]
            )
        )
    for group in by_fold.values():
        if len(group) > 1:
            for rec in group:
                detail = f"{len(group)} records: " + ", ".join(r.get("id") for r in group)
                violations["unique"].append({"id": rec.get("id"), "structure": rec["fields"].get("Structure"), "detail": detail})
                for r in records:
                    if r["id"] == rec.get("id") and "unique" not in r["ruleViolations"]:
                        r["ruleViolations"] = sorted(r["ruleViolations"] + ["unique"])

    def counts(key: str) -> dict:
        c = OrderedDict((s, 0) for s in ("exact", "case", "none", "missing"))
        for r in records:
            c[r[key]["status"]] += 1
        return c

    # Calibration: the per-name rules over the worksheet's own primary names (no Type, so the target and helper
    # rules use the worksheet target type: "Target" rows are targets).
    calibration = OrderedDict((rule, []) for rule in RULES if rule not in ("unique",))
    for row in worksheet["rows"]:
        name = row["primary"]
        if not name:
            continue
        wtype = "PTV" if row.get("target_type", "").strip().lower() == "target" else row.get("target_type")
        for rule in rule_findings(name, wtype):
            calibration[rule].append(name)

    return {
        "summary": OrderedDict(
            [
                ("records", len(records)),
                ("structure", counts("structureMatch")),
                ("tg263", counts("tg263Match")),
                ("tg263r", counts("tg263rMatch")),
                (
                    "structureUnmatchedByType",
                    OrderedDict(
                        sorted(
                            Counter(str(r["type"]) for r in records if r["structureMatch"]["status"] == "none").items(),
                            key=lambda kv: (-kv[1], kv[0]),
                        )
                    ),
                ),
                ("worksheetFmaidAvailable", sum(1 for r in records if r["worksheetFmaid"])),
                ("ruleViolations", OrderedDict((rule, len(v)) for rule, v in violations.items())),
                ("recordsWithAnyViolation", sum(1 for r in records if r["ruleViolations"])),
            ]
        ),
        "rules": RULES,
        "violations": violations,
        "helpersFound": helpers,
        "helperPrefixedTargets": prefixed_targets,
        "calibration": OrderedDict(
            [
                ("worksheetPrimaryNames", sum(1 for r in worksheet["rows"] if r["primary"])),
                ("violations", OrderedDict((rule, {"count": len(v), "names": v}) for rule, v in calibration.items())),
            ]
        ),
        "records": records,
    }


def md_name(name: str | None) -> str:
    if name is None or name == "":
        return "(empty)"
    return "`" + name.replace("|", "\\|") + "`"


def md_cell(text) -> str:
    return str(text).replace("|", "\\|") if text not in (None, "") else ""


STATUS_WORDS = {"exact": "exact", "case": "case only", "none": "no", "missing": "(empty)"}


def write_markdown(report: dict, out: Path) -> None:
    s = report["summary"]
    prov = report["provenance"]
    ws = prov["worksheet"]
    L = []
    L.append("# TG-263 nomenclature check (plan P1.8)")
    L.append("")
    L.append(
        f"Snapshot `{prov['snapshot']['path']}` ({s['records']} records) against the AAPM TG-263 worksheet "
        f"(`{ws['fileName']}`, SHA-256 `{ws['sha256']}`) and the in-repo TG-263 naming rules. Produced "
        f"{prov['generatedAtUtc']} by `tools/nomenclature/check_tg263_names.py`. The worksheet is not stored in this "
        "repository; only names from the snapshot, the matched worksheet spellings and FMAIDs appear here."
    )
    L.append("")
    L.append("## Summary")
    L.append("")
    L.append("| Snapshot field | Compared with | Exact | Ignoring case only | Unmatched | Empty |")
    L.append("| --- | --- | ---: | ---: | ---: | ---: |")
    for key, field, against in (
        ("structure", "`Structure`", "primary names"),
        ("tg263", "`TG_263`", "primary names"),
        ("tg263r", "`TG_263R`", "reverse-order names"),
    ):
        c = s[key]
        L.append(f"| {field} | {against} | {c['exact']} | {c['case']} | {c['none']} | {c['missing']} |")
    L.append("")
    L.append(
        "Unmatched `Structure` names by snapshot Type: "
        + ", ".join(f"{t} {n}" for t, n in s["structureUnmatchedByType"].items())
        + ". The worksheet lists only four target rows; target names are built from TG-263's pattern rules, so "
        "unmatched targets are expected."
    )
    L.append("")
    L.append(
        f"Worksheet FMAID available for {s['worksheetFmaidAvailable']} of {s['records']} records (through the "
        "`Structure` match, else the `TG_263` match). The snapshot has no FMAID column, so this is availability, "
        "not agreement."
    )
    L.append("")
    L.append("| Rule over `Structure` | Violations | Same rule over the worksheet's primary names |")
    L.append("| --- | ---: | ---: |")
    cal = report["calibration"]
    for rule, n in s["ruleViolations"].items():
        c = cal["violations"].get(rule)
        L.append(f"| `{rule}`: {RULE_LABELS[rule]} | {n} | {c['count'] if c else 'n/a'} |")
    L.append("")
    L.append(f"Records with at least one violation: {s['recordsWithAnyViolation']}. Rule definitions are under "
             "\"Rule violations\" below.")
    L.append("")

    L.append("## Worksheet provenance")
    L.append("")
    L.append("| Item | Value |")
    L.append("| --- | --- |")
    for label, value in (
        ("Resource page", ws.get("page")),
        ("URL", ws.get("url")),
        ("Retrieved (UTC)", ws.get("retrievedAtUtc")),
        ("HTTP Last-Modified", ws.get("lastModified")),
        ("Size (bytes)", f"{ws['size']:,}"),
        ("SHA-256", f"`{ws['sha256']}`"),
        ("Read with", f"xlrd {ws['xlrdVersion']}"),
        ("Sheets", ", ".join(f"`{n}`" for n in ws["sheetNames"])),
        ("Tab read", f"`{ws['sheet']}`"),
        ("Rows (incl. header)", ws["rowCount"]),
        ("Data rows with a name", ws["dataRowCount"]),
        ("Headers found", ", ".join(f"`{h}`" if h else "(blank)" for h in ws["headers"])),
    ):
        L.append(f"| {label} | {md_cell(value) if label not in ('SHA-256', 'Sheets', 'Tab read', 'Headers found') else value} |")
    L.append("")
    L.append("| Role | Header matched | Column | Matched on |")
    L.append("| --- | --- | ---: | --- |")
    for role, c in ws["columns"].items():
        if c:
            L.append(f"| {role} | `{c['header']}` | {c['index'] + 1} | \"{c['matchedOn']}\" |")
        else:
            L.append(f"| {role} | (not found) | | |")
    L.append("")
    snap = prov["snapshot"]
    L.append(
        f"Snapshot: `{snap['path']}`, {snap['size']:,} bytes, SHA-256 `{snap['sha256']}`, generated "
        f"{snap['generatedAtUtc']}, `recordCount` {snap['recordCount']}."
    )
    L.append("")

    recs = report["records"]
    L.append("## Unmatched and case-only names")
    L.append("")
    for key, field, against in (
        ("structureMatch", "Structure", "primary names"),
        ("tg263Match", "TG_263", "primary names"),
        ("tg263rMatch", "TG_263R", "reverse-order names"),
    ):
        src = {"structureMatch": "structure", "tg263Match": "tg263", "tg263rMatch": "tg263r"}[key]
        none = [r for r in recs if r[key]["status"] == "none"]
        case = [r for r in recs if r[key]["status"] == "case"]
        empty = [r for r in recs if r[key]["status"] == "missing"]
        L.append(f"### `{field}` against the worksheet {against}")
        L.append("")
        L.append(f"Unmatched ({len(none)}):")
        L.append("")
        L.append(", ".join(f"{md_name(r[src])} ({r['id']})" for r in none) or "none")
        L.append("")
        L.append(f"Matched ignoring case only ({len(case)}):")
        L.append("")
        L.append(", ".join(f"{md_name(r[src])} -> {md_name(r[key]['worksheetName'])} ({r['id']})" for r in case) or "none")
        L.append("")
        if empty:
            L.append(f"Empty ({len(empty)}): " + ", ".join(f"{md_name(r['structure'])} ({r['id']})" for r in empty))
            L.append("")

    L.append("## Rule violations")
    L.append("")
    for rule, items in report["violations"].items():
        L.append(f"### `{rule}` ({len(items)})")
        L.append("")
        L.append(report["rules"][rule])
        L.append("")
        if items:
            L.append("| Record id | Structure | Detail |")
            L.append("| --- | --- | --- |")
            for v in items:
                L.append(f"| {v['id']} | {md_name(v['structure'])} | {md_cell(v['detail'])} |")
        else:
            L.append("None.")
        L.append("")
    L.append(f"Records treated as helpers by the `helper_prefix` rule ({len(report['helpersFound'])}): "
             + (", ".join(f"{md_name(h['structure'])} (Type {h['type']})" for h in report["helpersFound"]) or "none") + ".")
    L.append("")
    pt = report["helperPrefixedTargets"]
    L.append(f"Targets exempt from the `target_prefix` rule because their name starts with a helper prefix ({len(pt)}): "
             + (", ".join(f"{md_name(t['structure'])} (Type {t['type']})" for t in pt) or "none") + ".")
    L.append("")
    L.append("### Calibration over the worksheet's primary names")
    L.append("")
    L.append(
        f"The per-name rules run over the {cal['worksheetPrimaryNames']} worksheet primary names (worksheet target type "
        "`Target` counts as a target). These are TG-263's own names, so a hit here shows where a rule as implemented is "
        "stricter than the report itself."
    )
    L.append("")
    for rule, c in cal["violations"].items():
        L.append(f"- `{rule}`: {c['count']}" + (": " + ", ".join(md_name(n) for n in c["names"]) if c["names"] else ""))
    L.append("")

    L.append("## Every record")
    L.append("")
    L.append("Match columns: exact, case only, no, or (empty). FMAID is the worksheet's, for the matched name.")
    L.append("")
    L.append("| Record id | Structure | Type | Structure match | TG_263 match | TG_263R match | Worksheet FMAID | Rules broken |")
    L.append("| --- | --- | --- | --- | --- | --- | --- | --- |")
    for r in recs:
        L.append(
            f"| {r['id']} | {md_name(r['structure'])} | {md_cell(r['type'])} | {STATUS_WORDS[r['structureMatch']['status']]} | "
            f"{STATUS_WORDS[r['tg263Match']['status']]} | {STATUS_WORDS[r['tg263rMatch']['status']]} | "
            f"{md_cell(r['worksheetFmaid'])} | {', '.join(r['ruleViolations'])} |"
        )
    L.append("")
    out.write_text("\n".join(L), encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--snapshot", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--worksheet", type=Path, help="local copy of the worksheet; no download when given")
    args = parser.parse_args(argv)

    snapshot_bytes = args.snapshot.read_bytes()
    snapshot = json.loads(snapshot_bytes.decode("utf-8"))
    tmp = None
    try:
        if args.worksheet:
            path = args.worksheet
            ws_prov = {"page": None, "url": None, "source": "local file (--worksheet)", "lastModified": None,
                       "retrievedAtUtc": None}
        else:
            tmp = Path(tempfile.mkdtemp(prefix="tg263-worksheet-"))
            path, headers = download_worksheet(tmp)
            ws_prov = {"page": WORKSHEET_PAGE, "url": WORKSHEET_URL, "source": "downloaded",
                       "lastModified": headers["lastModified"], "contentType": headers["contentType"],
                       "retrievedAtUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")}
        ws_prov.update({"fileName": path.name, "size": path.stat().st_size, "sha256": sha256_of(path)})
        worksheet = read_worksheet(path)
    finally:
        if tmp is not None:
            shutil.rmtree(tmp, ignore_errors=True)

    for key in ("xlrdVersion", "sheetNames", "sheet", "rowCount", "dataRowCount", "headers", "columns"):
        ws_prov[key] = worksheet[key]

    result = check(snapshot, worksheet)
    try:
        snap_path = args.snapshot.resolve().relative_to(Path.cwd().resolve()).as_posix()
    except ValueError:
        snap_path = args.snapshot.name
    report = OrderedDict(
        [
            (
                "provenance",
                OrderedDict(
                    [
                        ("tool", "tools/nomenclature/check_tg263_names.py"),
                        ("generatedAtUtc", datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")),
                        ("python", sys.version.split()[0]),
                        (
                            "snapshot",
                            OrderedDict(
                                [
                                    ("path", snap_path),
                                    ("size", len(snapshot_bytes)),
                                    ("sha256", hashlib.sha256(snapshot_bytes).hexdigest()),
                                    ("generatedAtUtc", snapshot.get("generatedAtUtc")),
                                    ("recordCount", snapshot.get("recordCount")),
                                ]
                            ),
                        ),
                        ("worksheet", ws_prov),
                    ]
                ),
            ),
        ]
    )
    report.update(result)

    args.out.mkdir(parents=True, exist_ok=True)
    (args.out / "nomenclature.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n"
    )
    write_markdown(report, args.out / "nomenclature.md")
    s = report["summary"]
    print(
        f"{s['records']} records; Structure exact {s['structure']['exact']}, case only {s['structure']['case']}, "
        f"unmatched {s['structure']['none']}; violations {dict(s['ruleViolations'])}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
