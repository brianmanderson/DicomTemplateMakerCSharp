# DicomTemplateMakerCSharp

Windows tools for building and applying DICOM RT Structure Set templates in radiation oncology contouring workflows. Define a structure template once — ROI names, colors, DICOM interpreted types, and FMA/SNOMED ontology codes — then generate pre-populated RT Structure Set files onto DICOM image series, so contouring starts from a standardized structure list instead of an empty one.

**Program download:** grab the compiled build from the [Releases](../../releases) section.
**Video walkthroughs:** [Our DICOM Playlist](https://www.youtube.com/playlist?list=PLudf8Cfe-LuctPJTTfgb2tDBUvyR-ZOQu) on YouTube.

## Components

| Project | What it is |
| --- | --- |
| `DicomTemplateMakerGUI` | WPF desktop app (the released program). Create/edit templates, assign ontology codes, pull shared template definitions from an Airtable base, and read/write Varian Eclipse structure-template XML. Includes a folder-watcher service for automated runs. |
| `DicomTemplateMakerCSharp` | Console runner. Scans template folders (each holding a `Paths.txt` plus an `ROIs/` folder of per-ROI text files), then walks the DICOM folder trees listed in each `Paths.txt` and writes an RT Structure file into them. |
| `CleaningRTStructureCsharp` | Console utility for cleaning existing RT Structure files. |
| `ROIOntologyClass` | Shared library: ROI and ontology-code classes, template folder loading (JSON and legacy text formats). |
| `Xaml_Maker` | Converts templates to/from Varian XML structure templates. |
| `AirTableRecords/` | Bundled per-treatment-site template definitions (e.g. `AbdPelv_Anal`, `AbdPelv_Bladder`) with one text file per ROI. |

## Shared template base (Airtable)

Templates can be maintained centrally in Airtable and pulled into the GUI. After logging into Airtable, copy the base from: https://airtable.com/shrk6XcfLggdTwetI

## Requirements

- Windows, .NET Framework 4.8
- Key NuGet packages: fo-dicom 5.0.3, Airtable client, Newtonsoft.Json, WindowsAPICodePack
