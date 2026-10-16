# TPS-realism report

Files: 53 RT Structure Set(s). Allowlist: `docs/evidence/validator-allowlist.json`. Gate: every error must be allowlisted; warnings are reported only. Result: **PASS**.

| Rule | Checks | Errors | Allowlisted | Warnings |
|---|---|---|---|---|
| RR1 | Structure Set ROI Sequence has at least one item | 0 | 0 | 0 |
| RR2 | the three ROI sequences use the same ROI numbers, without duplicates | 0 | 0 | 0 |
| RR3 | template ROIs have no Contour Sequence rather than an empty one | 0 | 0 | 0 |
| RR4 | one frame of reference, study and series; Contour Image Sequence lists each image once | 0 | 0 | 0 |
| RR5 | RT ROI Interpreted Type is a PS3.3 C.8.8.8 defined term or empty | 0 | 0 | 0 |
| RR6 | ROI names unique ignoring case and trailing spaces (error); over 16 characters (warning) | 0 | 0 | 2 |
| RR7 | Structure Set Label present, not empty, unique across the files | 0 | 0 | 0 |
| RR8 | code value syntax per scheme (SCT Verhoeff and partition, FMA integer); Code Value at most 16 characters | 9 | 9 | 0 |
| RR9 | context attributes as a unit; Varian context only on Varian codes; no retired ROI Observation Label (warnings) | 0 | 0 | 106 |
| RR10 | Frame of Reference UID and Position Reference Indicator present; one UID throughout | 0 | 0 | 0 |

## Errors not in the allowlist

none

## Allowlisted errors

- `RR8` D1b x1 in 1 file(s): ROI 'Eye_Origin_L': SCT code value '88' is not 6 to 18 digits.
- `RR8` D1b x1 in 1 file(s): ROI 'Eye_Origin_R': SCT code value '88' is not 6 to 18 digits.
- `RR8` D1b x1 in 1 file(s): ROI 'Fid': SCT code value '99' is not 6 to 18 digits.
- `RR8` D1b x1 in 1 file(s): ROI 'Hi HU': SCT code value '99' is not 6 to 18 digits.
- `RR8` D1b x2 in 2 file(s): ROI 'zPTV_High': SCT code value '88' is not 6 to 18 digits.
- `RR8` D1b x2 in 2 file(s): ROI 'zPTV_Mid': SCT code value '88' is not 6 to 18 digits.
- `RR8` D1b x1 in 1 file(s): ROI 'zPTVopt': SCT code value '88' is not 6 to 18 digits.

## Warnings (not gated)

- `RR6` x2 in 2 file(s): ROI name 'PTV_ChestWall_Eval' has 18 characters; TG-263 keeps names to 16.
- `RR9` x53 in 53 file(s): Codes of scheme SCT carry Varian context attributes (Context Identifier VMS011, Context UID 1.2.246.352.7.2.11, Mapping Resource 99VMS, Mapping Resource Name Varian Medical Systems, Mapping Resource UID 1.2.246.352.7.1.1); they describe Varian's structure code dictionary, not that scheme.
- `RR9` x53 in 53 file(s): ROI Observation Label (3006,0085), retired from the standard, is present in every RT ROI Observations item.
