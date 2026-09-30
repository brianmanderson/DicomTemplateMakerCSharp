using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using FellowOakDicom;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class RtStructureSetFileTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string WriteRt(string name, params (int Number, string Roi, string Color)[] rois)
    {
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage },
            { DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.Modality, "RTSTRUCT" },
        };
        dataset.Add(new DicomSequence(DicomTag.StructureSetROISequence, rois.Select(r => new DicomDataset { { DicomTag.ROINumber, r.Number }, { DicomTag.ROIName, r.Roi } }).ToArray()));
        dataset.Add(new DicomSequence(DicomTag.ROIContourSequence, rois.Select(r => new DicomDataset { { DicomTag.ReferencedROINumber, r.Number }, { DicomTag.ROIDisplayColor, r.Color.Split('\\') } }).ToArray()));
        dataset.Add(new DicomSequence(DicomTag.RTROIObservationsSequence, rois.Select(r => new DicomDataset
        {
            { DicomTag.ObservationNumber, r.Number },
            { DicomTag.ReferencedROINumber, r.Number },
            { DicomTag.RTROIInterpretedType, "ORGAN" },
        }).ToArray()));
        string path = Path.Combine(folder.Path, name);
        new DicomFile(dataset).Save(path);
        return path;
    }

    [Fact]
    public void An_rt_structure_set_is_read_into_the_template()
    {
        string rt = WriteRt("rt.dcm", (1, "Brain", "255\\0\\0"), (2, "Lung", "0\\255\\0"));
        var maker = new TemplateMaker();

        Assert.Null(RtStructureSetFile.Check(rt));
        Assert.True(RtStructureSetFile.TryInterpret(maker, rt, out string? error));

        Assert.Null(error);
        Assert.Equal(new[] { "Brain", "Lung" }, maker.ROIs.Select(r => r.ROIName));
    }

    [Fact]
    public void An_image_is_not_an_rt_structure_set()
    {
        string ct = Directory.GetFiles(Templates.RepositoryFile("DicomTemplateMakerGUI", "SmallCT")).First();
        var maker = new TemplateMaker();

        Assert.False(RtStructureSetFile.TryInterpret(maker, ct, out string? error));

        Assert.StartsWith(RtStructureSetFile.NotAnRtStructureSet, error, StringComparison.Ordinal);
        Assert.Contains("CT file", error, StringComparison.Ordinal);
        Assert.Empty(maker.ROIs);
    }

    [Fact]
    public void A_file_that_is_not_dicom_is_not_an_rt_structure_set()
    {
        string text = Path.Combine(folder.Path, "notes.dcm");
        File.WriteAllText(text, "These are my notes, not a DICOM file.");

        string? error = RtStructureSetFile.Check(text);

        Assert.NotNull(error);
        Assert.StartsWith(RtStructureSetFile.NotAnRtStructureSet, error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_file_is_not_an_rt_structure_set()
    {
        Assert.StartsWith(RtStructureSetFile.NotAnRtStructureSet, RtStructureSetFile.Check(Path.Combine(folder.Path, "gone.dcm")), StringComparison.Ordinal);
    }

    [Fact]
    public void An_rt_structure_set_without_roi_sequences_is_reported()
    {
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage },
            { DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.Modality, "RTSTRUCT" },
        };
        string path = Path.Combine(folder.Path, "empty.dcm");
        new DicomFile(dataset).Save(path);

        Assert.Contains("no ROIs", RtStructureSetFile.Check(path), StringComparison.Ordinal);
    }

    [Fact]
    public void A_damaged_rt_leaves_the_template_unchanged()
    {
        // The second colour does not fit in a byte, so interpret_RT fails after adding the first ROI.
        string rt = WriteRt("bad.dcm", (1, "Brain", "255\\0\\0"), (2, "Lung", "300\\0\\0"));
        var maker = new TemplateMaker();
        var existing = new ROIClass(1, 2, 3, "Heart", "ORGAN", new OntologyCodeClass("Heart", "7088", "FMA"));
        maker.ROIs.Add(existing);
        List<ROIClass> rois = maker.ROIs;
        Assert.Null(RtStructureSetFile.Check(rt));

        Assert.False(RtStructureSetFile.TryInterpret(maker, rt, out string? error));

        Assert.StartsWith("The RT Structure Set " + rt + " could not be read:", error, StringComparison.Ordinal);
        Assert.Contains("Nothing was added", error, StringComparison.Ordinal);
        Assert.Same(rois, maker.ROIs);
        Assert.Equal(new[] { existing }, maker.ROIs);
    }
}
