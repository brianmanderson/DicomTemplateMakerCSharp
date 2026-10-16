#!/usr/bin/env python3
"""Send generated RT Structure Sets to Orthanc over a real DICOM association and read them back.

Usage:
    python -I tools/dicom-validation/orthanc_smoke.py --rt-dir <folder> --out <folder>
        [--host localhost] [--dicom-port 4242] [--http-port 8042] [--aet ORTHANC]
        [--user orthanc] [--password orthanc] [--dry-run]

This is a transfer and read-back check, not a conformance oracle: Orthanc accepts files that dciodvfy and
validate_iods reject, so a pass here says only that the files survive a C-STORE and that an independent
parser (Orthanc, built on DCMTK) reads the ROI attributes back unchanged. Conformance is validate_rts.py's job.

What it does:
  1. Finds every RT Structure Set (Modality RTSTRUCT) under --rt-dir and, by SOP Instance UID, the CT slices
     each one references through its Contour Image Sequence (the CT files sit in the same tree).
  2. C-STOREs the CT slices, then the RTs, over one association with pynetdicom. The files are sent as they
     are on disk (no re-encoding); the RT Structure Set Storage and CT Image Storage presentation contexts are
     requested with Implicit and Explicit VR Little Endian. The status of every store is recorded.
  3. Through the REST API (/instances, /instances/{id}/simplified-tags, /instances/{id}/tags?simplify) finds
     each RT by SOP Instance UID and compares, per ROI number: ROIName (Structure Set ROI Sequence),
     ROIDisplayColor (ROI Contour Sequence), RTROIInterpretedType and the RT ROI Identification Code item's
     CodeValue, CodingSchemeDesignator and CodeMeaning (RT ROI Observations Sequence) with the source file read
     by pydicom. It also checks that the RT's referenced study, series and contour image UIDs resolve to CT
     instances stored in Orthanc.
  4. Writes orthanc.json and orthanc.md and exits 1 on any failed store, mismatch or problem.

--dry-run skips the network: it lists the RTs, their ROI counts and the CT slices they reference, and fails
only if a referenced CT slice is missing from the tree.

The script never reads, prints or writes attribute values of group 0010.
"""

from __future__ import annotations

import argparse
import base64
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

RT_STRUCT_STORAGE = "1.2.840.10008.5.1.4.1.1.481.3"
CT_IMAGE_STORAGE = "1.2.840.10008.5.1.4.1.1.2"
HEADER_TAGS = ["SOPClassUID", "SOPInstanceUID", "Modality", "StudyInstanceUID", "SeriesInstanceUID"]


def package_version(name: str) -> str:
    try:
        from importlib.metadata import version

        return version(name)
    except Exception as exc:  # noqa: BLE001
        return f"(unavailable: {exc})"


def clean(value) -> str | None:
    """A string value without DICOM padding, or None when the attribute is absent or empty."""
    if value is None:
        return None
    text = str(value).strip(" \x00")
    return text or None


def to_int(value) -> int | str | None:
    text = clean(value)
    if text is None:
        return None
    try:
        return int(text)
    except ValueError:
        return text  # kept as text so the mismatch shows what was read


def to_ints(value) -> list | None:
    """ROI Display Color as a list of ints, from pydicom's MultiValue or Orthanc's backslash-separated string."""
    if value is None:
        return None
    if isinstance(value, str):
        parts = value.split("\\")
    elif isinstance(value, (list, tuple)) or hasattr(value, "__iter__"):
        parts = list(value)
    else:
        parts = [value]
    out = [to_int(p) for p in parts]
    return out or None


# ---------------------------------------------------------------- attribute access, pydicom and Orthanc alike

def get(item, keyword: str):
    """Reads `keyword` from a pydicom Dataset or an Orthanc simplified-JSON dict; None when absent."""
    if isinstance(item, dict):
        return item.get(keyword)
    return item.get(keyword) if keyword in item else None


def seq(item, keyword: str) -> list:
    value = get(item, keyword)
    return list(value) if value else []


def roi_table(ds) -> tuple[dict[int, dict], list[str]]:
    """ROI number -> {name, color, type, code}, from either representation, plus structural problems."""
    problems: list[str] = []
    rois: dict[int, dict] = {}
    for item in seq(ds, "StructureSetROISequence"):
        number = to_int(get(item, "ROINumber"))
        if number in rois:
            problems.append(f"ROI number {number} appears twice in StructureSetROISequence")
        rois[number] = {"ROIName": clean(get(item, "ROIName")), "ROIDisplayColor": None,
                        "RTROIInterpretedType": None, "CodeValue": None, "CodingSchemeDesignator": None,
                        "CodeMeaning": None}
    for item in seq(ds, "ROIContourSequence"):
        number = to_int(get(item, "ReferencedROINumber"))
        if number not in rois:
            problems.append(f"ROIContourSequence references ROI number {number} not in StructureSetROISequence")
            continue
        rois[number]["ROIDisplayColor"] = to_ints(get(item, "ROIDisplayColor"))
    for item in seq(ds, "RTROIObservationsSequence"):
        number = to_int(get(item, "ReferencedROINumber"))
        if number not in rois:
            problems.append(f"RTROIObservationsSequence references ROI number {number} not in StructureSetROISequence")
            continue
        rois[number]["RTROIInterpretedType"] = clean(get(item, "RTROIInterpretedType"))
        codes = seq(item, "RTROIIdentificationCodeSequence")
        if codes:
            for key in ("CodeValue", "CodingSchemeDesignator", "CodeMeaning"):
                rois[number][key] = clean(get(codes[0], key))
    return rois, problems


def references(ds) -> dict:
    """The study, series and contour image SOP Instance UIDs an RT references."""
    studies, series, images = set(), set(), set()
    for frame in seq(ds, "ReferencedFrameOfReferenceSequence"):
        for study in seq(frame, "RTReferencedStudySequence"):
            studies.add(clean(get(study, "ReferencedSOPInstanceUID")))
            for ser in seq(study, "RTReferencedSeriesSequence"):
                series.add(clean(get(ser, "SeriesInstanceUID")))
                for image in seq(ser, "ContourImageSequence"):
                    images.add(clean(get(image, "ReferencedSOPInstanceUID")))
    return {"studies": sorted(s for s in studies if s), "series": sorted(s for s in series if s),
            "images": sorted(i for i in images if i)}


def compare(source: dict[int, dict], readback: dict[int, dict]) -> list[str]:
    mismatches = []
    for number in sorted(set(source) | set(readback), key=str):
        if number not in readback:
            mismatches.append(f"ROI {number}: missing in Orthanc read-back")
            continue
        if number not in source:
            mismatches.append(f"ROI {number}: in Orthanc read-back but not in the source file")
            continue
        for key, expected in source[number].items():
            actual = readback[number].get(key)
            if expected != actual:
                mismatches.append(f"ROI {number} {key}: source {expected!r}, Orthanc {actual!r}")
    return mismatches


# ---------------------------------------------------------------- local files

def scan(rt_dir: Path) -> tuple[list[Path], dict[str, Path], dict[Path, object], list[str]]:
    """Returns (RT paths, CT SOP Instance UID -> path, path -> header dataset, problems)."""
    import pydicom

    rts: list[Path] = []
    cts: dict[str, Path] = {}
    headers: dict[Path, object] = {}
    problems: list[str] = []
    for path in sorted(p for p in rt_dir.rglob("*.dcm") if p.is_file()):
        try:
            ds = pydicom.dcmread(str(path), stop_before_pixels=True, specific_tags=HEADER_TAGS)
        except Exception as exc:  # noqa: BLE001
            problems.append(f"{path.name}: unreadable ({type(exc).__name__})")
            continue
        headers[path] = ds
        modality = clean(ds.get("Modality"))
        if modality == "RTSTRUCT":
            rts.append(path)
        elif modality == "CT":
            cts[clean(ds.get("SOPInstanceUID"))] = path
    return rts, cts, headers, problems


def read_rt(path: Path):
    """The RT read with pydicom, restricted to the attributes compared, so group 0010 is never loaded."""
    import pydicom

    return pydicom.dcmread(str(path), specific_tags=HEADER_TAGS + [
        "StructureSetROISequence", "ROIContourSequence", "RTROIObservationsSequence",
        "ReferencedFrameOfReferenceSequence"])


# ---------------------------------------------------------------- network

class Rest:
    def __init__(self, host: str, port: int, user: str, password: str):
        self.base = f"http://{host}:{port}"
        token = base64.b64encode(f"{user}:{password}".encode()).decode()
        self.headers = {"Authorization": f"Basic {token}", "Accept": "application/json"}

    def get(self, path: str):
        request = urllib.request.Request(self.base + path, headers=self.headers)
        with urllib.request.urlopen(request, timeout=60) as response:
            return json.loads(response.read().decode("utf-8"))

    def wait(self, seconds: int = 120) -> dict:
        deadline = time.monotonic() + seconds
        while True:
            try:
                return self.get("/system")
            except (urllib.error.URLError, ConnectionError, OSError):
                if time.monotonic() > deadline:
                    raise
                time.sleep(2)


def store_all(paths: list[Path], host: str, port: int, aet: str) -> dict[Path, str]:
    """C-STOREs each file as it is on disk. Returns path -> status text ("0x0000" is success)."""
    from pydicom.uid import ExplicitVRLittleEndian, ImplicitVRLittleEndian
    from pynetdicom import AE

    ae = AE(ae_title="DTM_SMOKE")
    for context in (RT_STRUCT_STORAGE, CT_IMAGE_STORAGE):
        ae.add_requested_context(context, [ImplicitVRLittleEndian, ExplicitVRLittleEndian])
    statuses: dict[Path, str] = {}
    assoc = ae.associate(host, port, ae_title=aet)
    if not assoc.is_established:
        return {p: "association not established" for p in paths}
    try:
        for path in paths:
            if not assoc.is_established:
                statuses[path] = "association lost"
                continue
            try:
                status = assoc.send_c_store(path)
            except Exception as exc:  # noqa: BLE001
                statuses[path] = f"error: {type(exc).__name__}: {exc}"
                continue
            statuses[path] = f"0x{status.Status:04X}" if status and "Status" in status else "no response"
    finally:
        if assoc.is_established:
            assoc.release()
    return statuses


def orthanc_index(rest: Rest) -> dict[str, dict]:
    """SOP Instance UID -> {id, Modality, StudyInstanceUID, SeriesInstanceUID}; nothing else is kept."""
    index = {}
    for instance_id in rest.get("/instances"):
        tags = rest.get(f"/instances/{urllib.parse.quote(instance_id)}/simplified-tags")
        uid = clean(tags.get("SOPInstanceUID"))
        if uid:
            index[uid] = {"id": instance_id, "Modality": clean(tags.get("Modality")),
                          "StudyInstanceUID": clean(tags.get("StudyInstanceUID")),
                          "SeriesInstanceUID": clean(tags.get("SeriesInstanceUID"))}
    return index


def reference_problems(refs: dict, index: dict[str, dict]) -> list[str]:
    problems = []
    if not refs["images"]:
        problems.append("Orthanc read-back has no contour image references")
    for uid in refs["images"]:
        entry = index.get(uid)
        if entry is None:
            problems.append(f"referenced image {uid} is not stored in Orthanc")
            continue
        if entry["Modality"] != "CT":
            problems.append(f"referenced image {uid} has modality {entry['Modality']!r} in Orthanc, not CT")
        if entry["StudyInstanceUID"] not in refs["studies"]:
            problems.append(f"referenced image {uid} is in study {entry['StudyInstanceUID']}, not a referenced study")
        if entry["SeriesInstanceUID"] not in refs["series"]:
            problems.append(f"referenced image {uid} is in series {entry['SeriesInstanceUID']}, not a referenced series")
    stored_studies = {e["StudyInstanceUID"] for e in index.values() if e["Modality"] == "CT"}
    stored_series = {e["SeriesInstanceUID"] for e in index.values() if e["Modality"] == "CT"}
    problems += [f"referenced study {s} has no CT in Orthanc" for s in refs["studies"] if s not in stored_studies]
    problems += [f"referenced series {s} has no CT in Orthanc" for s in refs["series"] if s not in stored_series]
    return problems


# ---------------------------------------------------------------- main

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--rt-dir", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--host", default="localhost")
    parser.add_argument("--dicom-port", type=int, default=4242)
    parser.add_argument("--http-port", type=int, default=8042)
    parser.add_argument("--aet", default="ORTHANC")
    parser.add_argument("--user", default="orthanc")
    parser.add_argument("--password", default="orthanc")
    parser.add_argument("--dry-run", action="store_true", help="list the RTs and their CT slices; no network")
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)

    rts, cts, headers, problems = scan(args.rt_dir)
    if not rts:
        problems.append(f"no RT Structure Set under {args.rt_dir}")

    files: list[dict] = []
    sources: dict[Path, tuple] = {}
    needed_ct: dict[str, Path] = {}
    for path in rts:
        ds = read_rt(path)
        rois, structure = roi_table(ds)
        refs = references(ds)
        missing = [uid for uid in refs["images"] if uid not in cts]
        for uid in refs["images"]:
            if uid in cts:
                needed_ct[uid] = cts[uid]
        entry = {
            "file": path.name,
            "modality": "RTSTRUCT",
            "sopInstanceUID": clean(ds.get("SOPInstanceUID")),
            "roiCount": len(rois),
            "referencedCt": sorted(cts[uid].name for uid in refs["images"] if uid in cts),
            "missingCt": missing,
            "storeStatus": None,
            "roisCompared": 0,
            "mismatches": [f"source: {p}" for p in structure] + [f"referenced CT slice {u} not under --rt-dir" for u in missing],
        }
        if not refs["images"]:
            entry["mismatches"].append("source: no contour image references")
        files.append(entry)
        sources[path] = (entry, rois)

    ct_paths = sorted(set(needed_ct.values()))
    ct_entries = {p: {"file": p.name, "modality": "CT", "sopInstanceUID": clean(headers[p].get("SOPInstanceUID")),
                      "storeStatus": None} for p in ct_paths}
    system: dict = {}

    if not args.dry_run and rts:
        rest = Rest(args.host, args.http_port, args.user, args.password)
        try:
            system = rest.wait()
        except Exception as exc:  # noqa: BLE001
            problems.append(f"Orthanc REST API not reachable at {rest.base}: {type(exc).__name__}: {exc}")
        if system:
            statuses = store_all(ct_paths + rts, args.host, args.dicom_port, args.aet)
            for path, status in statuses.items():
                (ct_entries[path] if path in ct_entries else sources[path][0])["storeStatus"] = status
            index = orthanc_index(rest)
            for path in rts:
                entry, rois = sources[path]
                found = index.get(entry["sopInstanceUID"])
                if found is None:
                    entry["mismatches"].append("not found in Orthanc by SOP Instance UID")
                    continue
                tags = rest.get(f"/instances/{urllib.parse.quote(found['id'])}/tags?simplify")
                readback, structure = roi_table(tags)
                entry["roisCompared"] = len(rois)
                entry["mismatches"] += [f"Orthanc: {p}" for p in structure]
                entry["mismatches"] += compare(rois, readback)
                entry["mismatches"] += reference_problems(references(tags), index)

    all_files = list(ct_entries.values()) + files
    store_failures = [f for f in all_files if not args.dry_run and f["storeStatus"] != "0x0000"]
    mismatch_count = sum(len(f["mismatches"]) for f in files)
    summary = {
        "mode": "dry-run" if args.dry_run else "orthanc",
        "orthanc": {"Version": system.get("Version"), "DicomAet": system.get("DicomAet")} if system else None,
        "tools": {"pydicom": package_version("pydicom"), "pynetdicom": package_version("pynetdicom")},
        "rtCount": len(rts),
        "ctCount": len(ct_paths),
        "filesStored": 0 if args.dry_run else len(all_files) - len(store_failures),
        "storeFailures": len(store_failures),
        "roisInSource": sum(f["roiCount"] for f in files),
        "roisCompared": sum(f["roisCompared"] for f in files),
        "mismatches": mismatch_count,
        "problems": problems,
        "passed": not problems and not store_failures and mismatch_count == 0,
        "files": all_files,
    }
    (args.out / "orthanc.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    title = "Orthanc smoke test (dry run, no network)" if args.dry_run else "Orthanc smoke test"
    lines = [
        f"# {title}",
        "",
        "A transfer and read-back check, not a conformance oracle.",
        "",
        f"Result: **{'PASS' if summary['passed'] else 'FAIL'}**. "
        + (f"Orthanc {summary['orthanc']['Version']}; " if summary["orthanc"] else "")
        + f"pydicom {summary['tools']['pydicom']}, pynetdicom {summary['tools']['pynetdicom']}.",
        "",
        f"- RT Structure Sets: {len(rts)}; referenced CT slices: {len(ct_paths)}",
        f"- Files stored: {summary['filesStored']} of {len(all_files)}; store failures: {len(store_failures)}",
        f"- ROIs in source: {summary['roisInSource']}; ROIs compared after read-back: {summary['roisCompared']}",
        f"- Mismatches: {mismatch_count}",
    ]
    if problems:
        lines += ["", "## Problems", ""] + [f"- {p}" for p in problems]
    lines += ["", "## Store failures", ""]
    lines += [f"- {f['file']}: {f['storeStatus']}" for f in store_failures] or ["none"]
    lines += ["", "## Mismatches", ""]
    lines += [f"- {f['file']}: {m}" for f in files for m in f["mismatches"]] or ["none"]
    lines += ["", "## Files", "", "| File | Modality | ROIs | Store | Mismatches | Referenced CT |", "|---|---|---|---|---|---|"]
    for f in all_files:
        lines.append(f"| {f['file']} | {f['modality']} | {f.get('roiCount', '')} | {f['storeStatus'] or '-'} | "
                     f"{len(f.get('mismatches', []))} | {', '.join(f.get('referencedCt', []))} |")
    (args.out / "orthanc.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    print("\n".join(lines[: lines.index("## Files") - 1]))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
