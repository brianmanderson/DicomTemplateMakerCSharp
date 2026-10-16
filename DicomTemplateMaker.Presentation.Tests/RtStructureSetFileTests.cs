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

    private string WriteRt(string name, params (int Number, string Roi, string Color)[] rois) =>
        WriteRt(name, rois.Select(r => (r.Number, r.Roi, r.Color, (string?)("Code" + r.Number))).ToArray());

    /// <summary>An RT whose ROIs have a bare SCT code (value, scheme and meaning only) when <c>Code</c> is not null.</summary>
    private string WriteRt(string name, params (int Number, string Roi, string Color, string? Code)[] rois)
    {
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage },
            { DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.Modality, "RTSTRUCT" },
        };
        dataset.Add(new DicomSequence(DicomTag.StructureSetROISequence, rois.Select(r => new DicomDataset { { DicomTag.ROINumber, r.Number }, { DicomTag.ROIName, r.Roi } }).ToArray()));
        dataset.Add(new DicomSequence(DicomTag.ROIContourSequence, rois.Select(r => new DicomDataset { { DicomTag.ReferencedROINumber, r.Number }, { DicomTag.ROIDisplayColor, r.Color.Split('\\') } }).ToArray()));
        dataset.Add(new DicomSequence(DicomTag.RTROIObservationsSequence, rois.Select(r =>
        {
            var observation = new DicomDataset
            {
                { DicomTag.ObservationNumber, r.Number },
                { DicomTag.ReferencedROINumber, r.Number },
                { DicomTag.RTROIInterpretedType, "ORGAN" },
            };
            if (r.Code != null)
            {
                observation.Add(new DicomSequence(DicomTag.RTROIIdentificationCodeSequence, new DicomDataset
                {
                    { DicomTag.CodeValue, r.Code },
                    { DicomTag.CodingSchemeDesignator, "SCT" },
                    { DicomTag.CodeMeaning, r.Roi },
                }));
            }
            return observation;
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
        Assert.Equal(new (string, string?, string?, string?)[] { ("Brain", "Code1", "SCT", null), ("Lung", "Code2", "SCT", null) },
            maker.ROIs.Select(r => (r.Ontology_Class!.CodeMeaning, r.Ontology_Class.CodeValue, r.Ontology_Class.Scheme, r.Ontology_Class.ContextIdentifier)));
    }

    [Fact]
    public void An_roi_without_a_code_is_skipped_with_the_reason()
    {
        string rt = WriteRt("rt.dcm", (1, "Brain", "255\\0\\0", "12738006"), (2, "Ring", "0\\255\\0", null));
        var maker = new TemplateMaker();

        Assert.True(RtStructureSetFile.TryInterpret(maker, rt, out string? error, out IReadOnlyList<string> skipped));

        Assert.Null(error);
        Assert.Equal(new[] { "Brain" }, maker.ROIs.Select(r => r.ROIName));
        string reason = Assert.Single(skipped);
        Assert.StartsWith("Ring (ROI number 2): ", reason, StringComparison.Ordinal);
        Assert.Contains("no RT ROI Identification Code", reason, StringComparison.Ordinal);
        Assert.Contains("Ring (ROI number 2)", RtStructureSetFile.DescribeSkipped(skipped), StringComparison.Ordinal);
        Assert.Equal(string.Empty, RtStructureSetFile.DescribeSkipped(Array.Empty<string>()));
    }

    [Fact]
    public void An_rt_without_a_readable_roi_is_reported_with_the_reasons()
    {
        string rt = WriteRt("rt.dcm", (1, "Ring", "255\\0\\0", null), (2, "Couch", "0\\255\\0", null));
        var maker = new TemplateMaker();
        var existing = new ROIClass(1, 2, 3, "Heart", "ORGAN", new OntologyCodeClass("Heart", "7088", "FMA"));
        maker.ROIs.Add(existing);

        Assert.False(RtStructureSetFile.TryInterpret(maker, rt, out string? error, out IReadOnlyList<string> skipped));

        Assert.StartsWith("No ROI of the RT Structure Set " + rt + " could be read:", error, StringComparison.Ordinal);
        Assert.Contains("Ring (ROI number 1)", error, StringComparison.Ordinal);
        Assert.Contains("Couch (ROI number 2)", error, StringComparison.Ordinal);
        Assert.Contains("Nothing was added", error, StringComparison.Ordinal);
        Assert.Equal(2, skipped.Count);
        Assert.Equal(new[] { existing }, maker.ROIs);
        Assert.Empty(maker.Ontologies);
    }

    [Fact]
    public void A_null_reference_while_reading_is_reported_and_leaves_the_template_unchanged()
    {
        // A null entry in the ontology list makes the code lookup throw NullReferenceException; it used to escape TryInterpret.
        string rt = WriteRt("rt.dcm", (1, "Brain", "255\\0\\0"));
        var maker = new TemplateMaker();
        var heart = new OntologyCodeClass("Heart", "7088", "FMA");
        maker.Ontologies.Add(heart);
        maker.Ontologies.Add(null!);

        Assert.False(RtStructureSetFile.TryInterpret(maker, rt, out string? error));

        Assert.StartsWith("The RT Structure Set " + rt + " could not be read: an attribute that is needed is missing", error, StringComparison.Ordinal);
        Assert.Contains(nameof(NullReferenceException), error, StringComparison.Ordinal);
        Assert.Contains("Nothing was added", error, StringComparison.Ordinal);
        Assert.Empty(maker.ROIs);
        Assert.Equal(new[] { heart, null }, maker.Ontologies);
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
