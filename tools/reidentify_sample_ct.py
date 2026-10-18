"""Give the bundled sample CT a fictitious test patient (overhaul plan step P0.2).

The four slices in ``DicomTemplateMakerGUI/SmallCT`` (``vhm.1001.dcm`` to ``vhm.1004.dcm``, Explicit VR Little
Endian, no Specific Character Set) carried a real person's name and ID, a birth date and a birth time.
``RtStructureBuilder`` copies the patient attributes into every RT Structure Set it writes, so every test RT and
every file the CI validation job produces carried that identity. This script rewrites the four files in place.

What it changes in every slice:

- ``PatientName`` (0010,0010) = ``ZZTEST^TG263TEMPLATE``
- ``PatientID`` (0010,0020) = ``ZZTEST-0001``
- ``PatientBirthDate`` (0010,0030) and ``PatientBirthTime`` (0010,0032) present and empty
- ``PatientIdentityRemoved`` (0012,0062) = ``YES`` (added)
- ``DeidentificationMethod`` (0012,0063) = ``DicomTemplateMakerCSharp tools/reidentify_sample_ct.py`` (added)
- ``ReferringPhysicianName`` (0008,0090) present and empty
- ``SeriesDescription`` (0008,103E) = ``Sample CT`` (it named an institution's residency template)
- ``InstitutionName`` (0008,0080) = ``Brian Anderson Fictional Hospital``, a fictitious institution
- ``InstitutionAddress`` (0008,0081) removed (it was the GDCM sample-data web page, which describes no institution)
- ``StudyInstanceUID``, ``SeriesInstanceUID`` and ``FrameOfReferenceUID``: one new ``2.25.`` UID each, shared by
  all four slices (one study, one series, one frame of reference, as before)
- ``SOPInstanceUID``: a new, distinct ``2.25.`` UID per slice, with File Meta ``MediaStorageSOPInstanceUID``
  equal to it

Everything else is kept as it was: ``PatientSex``, pixel data, geometry, study/instance dates and times,
``StudyDescription``, ``Manufacturer`` (``GDCM``), the other file meta elements and the transfer syntax. The slices
have no private attributes and no station name, device serial number, operator or other person name.

The new UIDs are UUID-derived (``2.25.`` followed by the UUID as a decimal integer, PS3.5 B.2). They are derived
with UUID version 5 from a fixed namespace and the role of the UID, so the script is re-runnable: running it
again on the rewritten files leaves them byte-identical. Before writing, it checks that the previous name, ID,
series description and institution name and address appear in no written file (the values are read from the
files and never printed).

Run from the repository root with ``python -I tools/reidentify_sample_ct.py``.
"""

from __future__ import annotations

import sys
import uuid
from pathlib import Path

import pydicom
from pydicom.dataset import Dataset
from pydicom.filebase import DicomBytesIO

CT_DIR = Path(__file__).resolve().parent.parent / "DicomTemplateMakerGUI" / "SmallCT"
SLICES = ["vhm.1001.dcm", "vhm.1002.dcm", "vhm.1003.dcm", "vhm.1004.dcm"]

PATIENT_NAME = "ZZTEST^TG263TEMPLATE"
PATIENT_ID = "ZZTEST-0001"
DEIDENTIFICATION_METHOD = "DicomTemplateMakerCSharp tools/reidentify_sample_ct.py"
SERIES_DESCRIPTION = "Sample CT"
INSTITUTION_NAME = "Brian Anderson Fictional Hospital"

# Fixed namespace for this script's UIDs, so every run derives the same values.
UID_NAMESPACE = uuid.uuid5(uuid.NAMESPACE_URL, "https://github.com/brianmanderson/DicomTemplateMakerCSharp/SmallCT")


def uid_for(role: str) -> str:
    return f"2.25.{uuid.uuid5(UID_NAMESPACE, role).int}"


STUDY_UID = uid_for("study")
SERIES_UID = uid_for("series")
FRAME_OF_REFERENCE_UID = uid_for("frame-of-reference")


def encode(ds: Dataset) -> bytes:
    buffer = DicomBytesIO()
    ds.save_as(buffer, enforce_file_format=False)
    return buffer.getvalue()


def reidentify(ds: Dataset, sop_instance_uid: str) -> None:
    ds.PatientName = PATIENT_NAME
    ds.PatientID = PATIENT_ID
    ds.PatientBirthDate = ""
    ds.PatientBirthTime = ""
    ds.PatientIdentityRemoved = "YES"
    ds.DeidentificationMethod = DEIDENTIFICATION_METHOD
    ds.ReferringPhysicianName = ""
    ds.SeriesDescription = SERIES_DESCRIPTION
    ds.InstitutionName = INSTITUTION_NAME
    if "InstitutionAddress" in ds:
        del ds.InstitutionAddress
    ds.StudyInstanceUID = STUDY_UID
    ds.SeriesInstanceUID = SERIES_UID
    ds.FrameOfReferenceUID = FRAME_OF_REFERENCE_UID
    ds.SOPInstanceUID = sop_instance_uid
    ds.file_meta.MediaStorageSOPInstanceUID = sop_instance_uid


def main() -> int:
    originals: dict[Path, Dataset] = {}
    previous_identity: set[bytes] = set()
    for name in SLICES:
        path = CT_DIR / name
        ds = pydicom.dcmread(path)
        originals[path] = ds
        if str(ds.PatientName) != PATIENT_NAME:
            raw_name = str(ds.PatientName)
            previous_identity.update(
                v.encode("latin-1").lower() for v in (raw_name, raw_name.replace("^", " ")) if v.strip())
        if ds.PatientID != PATIENT_ID and ds.PatientID.strip():
            previous_identity.add(ds.PatientID.encode("latin-1").lower())
        for keyword, new_value in (("SeriesDescription", SERIES_DESCRIPTION), ("InstitutionName", INSTITUTION_NAME),
                                   ("InstitutionAddress", None)):
            old_value = str(ds.get(keyword, "")).strip()
            if old_value and old_value != new_value:
                previous_identity.add(old_value.encode("latin-1").lower())

    written: dict[Path, bytes] = {}
    for path, ds in originals.items():
        transfer_syntax = ds.file_meta.TransferSyntaxUID
        pixels = ds.PixelData
        reidentify(ds, uid_for(f"sop-instance/{path.name}"))
        data = encode(ds)

        check = pydicom.dcmread(DicomBytesIO(data))
        if check.file_meta.TransferSyntaxUID != transfer_syntax or check.PixelData != pixels:
            raise SystemExit(f"{path.name}: transfer syntax or pixel data would change")
        lowered = data.lower()
        if any(value in lowered for value in previous_identity):
            raise SystemExit(f"{path.name}: the previous identity would still be in the file")
        written[path] = data

    sop_uids = {pydicom.dcmread(DicomBytesIO(d)).SOPInstanceUID for d in written.values()}
    if len(sop_uids) != len(SLICES):
        raise SystemExit("SOP Instance UIDs are not distinct")

    for path, data in written.items():
        unchanged = path.read_bytes() == data
        if not unchanged:
            path.write_bytes(data)
        print(f"{path.name}: {'unchanged' if unchanged else 'rewritten'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
