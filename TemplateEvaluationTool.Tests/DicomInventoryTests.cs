using FellowOakDicom;
using Xunit;

namespace TemplateEvaluationTool.Tests;

public sealed class DicomInventoryTests
{
    private const string StudyUid = "1.2.826.0.1.3680043.2.1125.1.1";
    private const string InstanceUid = "1.2.826.0.1.3680043.2.1125.1.2";

    /// <summary>Saves and reopens the file, so the inventory sees stored lengths and real file meta information.</summary>
    private static List<string> Inventory(DicomDataset dataset)
    {
        using var stream = new MemoryStream();
        new DicomFile(dataset).Save(stream);
        stream.Position = 0;
        return DicomInventory.Write(DicomFile.Open(stream, FileReadOption.ReadAll)).TrimEnd('\n').Split('\n').ToList();
    }

    private static DicomDataset Dataset(params DicomItem[] extra)
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage },
            { DicomTag.SOPInstanceUID, InstanceUid },
        };
        dataset.Add(extra);
        return dataset;
    }

    [Fact]
    public void Patient_attributes_person_names_and_the_implementation_identity_are_masked_with_their_lengths()
    {
        List<string> lines = Inventory(Dataset(
            new DicomPersonName(DicomTag.PatientName, "Doe^John"),
            new DicomLongString(DicomTag.PatientID, "123456"),
            new DicomDate(DicomTag.PatientBirthDate, "19500101"),
            new DicomCodeString(DicomTag.PatientSex, "M"),
            new DicomTime(DicomTag.PatientBirthTime, "101010"),
            new DicomPersonName(DicomTag.ReferringPhysicianName, "Smith"),
            new DicomPersonName(DicomTag.ROIInterpreter, "Program_Constant")));

        Assert.Contains("(0002,0012) ImplementationClassUID UI 1 * <MASKED>", lines);
        Assert.Contains("(0002,0013) ImplementationVersionName SH 1 * <MASKED>", lines);
        Assert.Contains("(0010,0010) PatientName PN 1 * <MASKED>", lines);
        Assert.Contains("(0010,0020) PatientID LO 1 * <MASKED>", lines);
        Assert.Contains("(0010,0030) PatientBirthDate DA 1 * <MASKED>", lines);
        Assert.Contains("(0010,0040) PatientSex CS 1 * <MASKED>", lines);
        Assert.Contains("(0010,0032) PatientBirthTime TM 1 * <MASKED>", lines);
        Assert.Contains("(0008,0090) ReferringPhysicianName PN 1 * <MASKED>", lines);
        Assert.Contains("(3006,00A6) ROIInterpreter PN 1 16 Program_Constant", lines);
        string all = string.Join("\n", lines);
        Assert.DoesNotContain("Doe", all, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", all, StringComparison.Ordinal);
        Assert.DoesNotContain("19500101", all, StringComparison.Ordinal);
        Assert.DoesNotContain("Smith", all, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_values_stay_empty_with_their_real_length()
    {
        List<string> lines = Inventory(Dataset(new DicomPersonName(DicomTag.PatientName, string.Empty), new DicomDate(DicomTag.StudyDate, Array.Empty<string>())));

        Assert.Contains("(0010,0010) PatientName PN 0 0", lines);
        Assert.Contains("(0008,0020) StudyDate DA 0 0", lines);
    }

    [Fact]
    public void Dates_and_times_become_placeholders()
    {
        List<string> lines = Inventory(Dataset(
            new DicomDate(DicomTag.StudyDate, "20261015"),
            new DicomTime(DicomTag.StudyTime, "101010.123"),
            new DicomDateTime(DicomTag.ContextGroupVersion, "20161209")));

        Assert.Contains("(0008,0020) StudyDate DA 1 * <DATE>", lines);
        Assert.Contains("(0008,0030) StudyTime TM 1 * <TIME>", lines);
        Assert.Contains("(0008,0106) ContextGroupVersion DT 1 * <DATETIME>", lines);
    }

    [Fact]
    public void Each_distinct_uid_is_numbered_in_order_of_first_appearance_and_standard_uids_are_named()
    {
        var item = new DicomDataset
        {
            { DicomTag.ReferencedSOPClassUID, DicomUID.CTImageStorage },
            { DicomTag.ReferencedSOPInstanceUID, StudyUid },
        };
        List<string> lines = Inventory(Dataset(
            new DicomUniqueIdentifier(DicomTag.StudyInstanceUID, StudyUid),
            new DicomSequence(DicomTag.ReferencedImageSequence, item, item.Clone())));

        Assert.Contains("(0002,0003) MediaStorageSOPInstanceUID UI 1 * UID#1", lines);
        Assert.Contains("(0008,0018) SOPInstanceUID UI 1 * UID#1", lines);
        Assert.Contains("(0020,000D) StudyInstanceUID UI 1 * UID#2", lines);
        Assert.Contains("(0002,0002) MediaStorageSOPClassUID UI 1 30 1.2.840.10008.5.1.4.1.1.481.3 (RT Structure Set Storage)", lines);
        Assert.Contains("(0008,1140) ReferencedImageSequence SQ 2 -", lines);
        Assert.Contains("  (FFFE,E000) Item 2", lines);
        Assert.Contains("    (0008,1150) ReferencedSOPClassUID UI 1 26 1.2.840.10008.5.1.4.1.1.2 (CT Image Storage)", lines);
        Assert.Equal(2, lines.Count(l => l == "    (0008,1155) ReferencedSOPInstanceUID UI 1 * UID#2"));
    }

    [Fact]
    public void File_meta_comes_first_and_other_values_keep_their_stored_length_and_value()
    {
        List<string> lines = Inventory(Dataset(
            new DicomLongString(DicomTag.StructureSetLabel, "Abc"),
            new DicomIntegerString(DicomTag.ROIDisplayColor, 255, 0, 10),
            new DicomUnsignedShort(DicomTag.Rows, 512)));

        Assert.StartsWith("(0002,", lines[0], StringComparison.Ordinal);
        Assert.True(lines.FindLastIndex(l => l.StartsWith("(0002,", StringComparison.Ordinal)) < lines.FindIndex(l => l.StartsWith("(0008,", StringComparison.Ordinal)));
        Assert.Contains("(3006,0002) StructureSetLabel LO 1 4 Abc", lines);
        Assert.Contains("(3006,002A) ROIDisplayColor IS 3 8 255\\0\\10", lines);
        Assert.Equal("(0002,0000) FileMetaInformationGroupLength UL 1 4 <DERIVED>", lines[0]);
        Assert.Equal("(0002,0001) FileMetaInformationVersion OB 1 2 0001", lines[1]);
        Assert.Contains("(0028,0010) Rows US 1 2 512", lines);
    }

    [Fact]
    public void Uids_in_the_run_report_are_masked_inside_file_names_and_the_output_folder_is_hidden()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "evaluation-out");
        var report = new Newtonsoft.Json.Linq.JObject
        {
            ["Path"] = Path.Combine(outDir, "series", "Lung_UID1.2.826.0.1.99.dcm"),
            ["SeriesInstanceUid"] = "1.2.826.0.1.99",
            ["Other"] = "version 1.1.0 and 1.2.840.10008.1.2",
            ["StartedAt"] = "2026-10-15T10:00:00Z",
        };

        RunReportMasking.Mask(report, outDir);

        Assert.Equal("<out>/series/Lung_UIDUID#1.dcm", (string?)report["Path"]);
        Assert.Equal("UID#1", (string?)report["SeriesInstanceUid"]);
        Assert.Equal("version 1.1.0 and 1.2.840.10008.1.2 (Implicit VR Little Endian: Default Transfer Syntax for DICOM)", (string?)report["Other"]);
        Assert.Equal("<DATETIME>", (string?)report["StartedAt"]);
    }

    [Fact]
    public void Uuid_derived_uids_in_the_run_report_are_masked()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "evaluation-out");
        const string seriesUid = "2.25.279807263898541607461237963382274624702";
        var report = new Newtonsoft.Json.Linq.JObject
        {
            ["Path"] = Path.Combine(outDir, "series", $"Lung_UID{seriesUid}.dcm"),
            ["SeriesInstanceUid"] = seriesUid,
            ["Other"] = "2.25.1.2 is not a UUID-derived UID but has four components",
        };

        RunReportMasking.Mask(report, outDir);

        Assert.Equal("<out>/series/Lung_UIDUID#1.dcm", (string?)report["Path"]);
        Assert.Equal("UID#1", (string?)report["SeriesInstanceUid"]);
        Assert.Equal("UID#2 is not a UUID-derived UID but has four components", (string?)report["Other"]);
    }
}
