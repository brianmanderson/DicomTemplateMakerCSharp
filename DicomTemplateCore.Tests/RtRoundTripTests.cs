using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Services;
using FellowOakDicom;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>
/// Every snapshot site, built into a template, written as an RT on the sample CT and read back with interpret_RT,
/// gives back each ROI's name, colour, interpreted type and code. Generated files stay in a temporary folder: the
/// sample CT's patient data must not end up in the repository.
/// </summary>
public class RtRoundTripTests
{
    private static readonly DateTimeOffset GenerationTime = new DateTimeOffset(2031, 3, 4, 22, 30, 15, TimeSpan.Zero);

    public static TheoryData<string> Sites => new TheoryData<string>(SnapshotSites.SiteNames);

    private static List<DicomDataset> SmallCt() =>
        Directory.GetFiles(TestFolder.Data("SmallCT")).Order(StringComparer.Ordinal).Select(f => DicomFile.Open(f).Dataset).ToList();

    private static (string Name, byte R, byte G, byte B, string? Type, string Meaning, string? Value, string? Scheme) Summary(ROIClass roi) =>
        (roi.ROIName, roi.R, roi.G, roi.B, roi.ROI_Interpreted_type, roi.Ontology_Class!.CodeMeaning, roi.Ontology_Class.CodeValue, roi.Ontology_Class.Scheme);

    [Fact]
    public void The_snapshot_has_sites()
    {
        Assert.NotEmpty(SnapshotSites.SiteNames);
    }

    [Theory]
    [MemberData(nameof(Sites))]
    public void A_snapshot_site_survives_template_rt_and_interpret(string site)
    {
        using var root = new TestFolder();
        string library = root.Sub("Ontologies");
        string template = Path.Combine(root.Path, "Template");
        SnapshotSites.MakeTemplate(template, library, SnapshotSites.BuildRois(site));

        var opened = new TemplateMaker { Ontologies = OntologyTools.LoadOntologiesFromFolder(library) };
        opened.define_path(template);
        opened.categorize_folder();
        Assert.NotEmpty(opened.ROIs);

        RtBuildResult result = new RtStructureBuilder(DicomFile.Open(DicomTemplateRunner.DefaultTemplateRsPath).Dataset)
            .Build(SmallCt(), site, opened.ROIs, GenerationTime);
        Assert.Null(result.Error);
        // Only ROIs without a complete code are left out of an RT, and the builder says so.
        HashSet<string> leftOut = result.RoiFailures.Select(f => f.RoiName).ToHashSet(StringComparer.Ordinal);
        foreach (ROIClass roi in opened.ROIs.Where(r => leftOut.Contains(r.ROIName)))
        {
            Assert.NotNull(RtStructureBuilder.MissingCodeParts(roi.Ontology_Class!));
        }

        string rt = Path.Combine(root.Path, "RS.dcm");
        Assert.IsType<DicomFile>(result.File).Save(rt);
        var reader = new TemplateMaker();
        Assert.Equal(result.RoiCount, reader.interpret_RT(rt));

        Assert.Empty(reader.SkippedRois);
        var expected = opened.ROIs.Where(r => !leftOut.Contains(r.ROIName)).Select(roi => Summary(roi) with
        {
            // As the RT holds them; see RtStructureBuilder.
            Name = RtStructureBuilder.RtRoiName(roi.ROIName),
            Type = roi.ROI_Interpreted_type!.ToUpperInvariant(),
            Meaning = RtStructureBuilder.RtCodeMeaning(roi.Ontology_Class!.CodeMeaning, roi.ROIName)!,
        });
        Assert.Equal(expected, reader.ROIs.Select(Summary));
    }

    [Fact]
    public void An_rt_with_bare_sct_codes_reads()
    {
        // Only Code Value, Coding Scheme Designator and Code Meaning: no context or mapping attributes.
        using var root = new TestFolder();
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage },
            { DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.Modality, "RTSTRUCT" },
        };
        dataset.Add(new DicomSequence(DicomTag.StructureSetROISequence,
            new DicomDataset { { DicomTag.ROINumber, 1 }, { DicomTag.ROIName, "PTV_High" } },
            new DicomDataset { { DicomTag.ROINumber, 2 }, { DicomTag.ROIName, "PTV_Low" } },
            new DicomDataset { { DicomTag.ROINumber, 3 }, { DicomTag.ROIName, "Couch" } }));
        dataset.Add(new DicomSequence(DicomTag.ROIContourSequence,
            new DicomDataset { { DicomTag.ReferencedROINumber, 1 }, { DicomTag.ROIDisplayColor, "255", "0", "0" } },
            new DicomDataset { { DicomTag.ReferencedROINumber, 2 }, { DicomTag.ROIDisplayColor, "0", "255", "0" } },
            new DicomDataset { { DicomTag.ReferencedROINumber, 3 }, { DicomTag.ROIDisplayColor, "0", "0", "255" } }));
        dataset.Add(new DicomSequence(DicomTag.RTROIObservationsSequence,
            Observation(1, "PTV", new DicomDataset { { DicomTag.CodeValue, "228793007" }, { DicomTag.CodingSchemeDesignator, "SCT" }, { DicomTag.CodeMeaning, "PTV_High" } }),
            Observation(2, "PTV", new DicomDataset { { DicomTag.CodeValue, "228793007" }, { DicomTag.CodingSchemeDesignator, "SCT" }, { DicomTag.CodeMeaning, "PTV_Low" } }),
            Observation(3, "SUPPORT", null)));
        string rt = Path.Combine(root.Path, "bare.dcm");
        new DicomFile(dataset).Save(rt);

        var reader = new TemplateMaker();
        Assert.Equal(2, reader.interpret_RT(rt));

        Assert.Equal(new (string, byte, byte, byte, string?, string, string?, string?)[]
        {
            ("PTV_High", 255, 0, 0, "PTV", "PTV_High", "228793007", "SCT"),
            ("PTV_Low", 0, 255, 0, "PTV", "PTV_Low", "228793007", "SCT"),
        }, reader.ROIs.Select(Summary));
        Assert.All(reader.ROIs, roi =>
        {
            OntologyCodeClass code = roi.Ontology_Class!;
            Assert.Null(code.ContextIdentifier);
            Assert.Null(code.ContextGroupVersion);
            Assert.Null(code.ContextUID);
            Assert.Null(code.MappingResource);
            Assert.Null(code.MappingResourceName);
            Assert.Null(code.MappingResourceUID);
        });
        Assert.Equal(2, reader.Ontologies.Count);
        string skipped = Assert.Single(reader.SkippedRois);
        Assert.StartsWith("Couch (ROI number 3): it has no RT ROI Identification Code", skipped, StringComparison.Ordinal);
    }

    [Fact]
    public void Rois_without_an_observation_or_a_complete_code_are_skipped_with_the_reason()
    {
        using var root = new TestFolder();
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage },
            { DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.Modality, "RTSTRUCT" },
        };
        dataset.Add(new DicomSequence(DicomTag.StructureSetROISequence,
            new DicomDataset { { DicomTag.ROINumber, 1 }, { DicomTag.ROIName, "Brain" } },
            new DicomDataset { { DicomTag.ROINumber, 2 }, { DicomTag.ROIName, "NoObservation" } },
            new DicomDataset { { DicomTag.ROINumber, 3 }, { DicomTag.ROIName, "NoScheme" } },
            new DicomDataset { { DicomTag.ROINumber, 4 }, { DicomTag.ROIName, "NoColour" } }));
        dataset.Add(new DicomSequence(DicomTag.ROIContourSequence,
            new DicomDataset { { DicomTag.ReferencedROINumber, 1 }, { DicomTag.ROIDisplayColor, "255", "0", "0" } },
            new DicomDataset { { DicomTag.ReferencedROINumber, 2 }, { DicomTag.ROIDisplayColor, "0", "255", "0" } },
            new DicomDataset { { DicomTag.ReferencedROINumber, 3 }, { DicomTag.ROIDisplayColor, "0", "0", "255" } },
            new DicomDataset { { DicomTag.ReferencedROINumber, 4 } }));
        dataset.Add(new DicomSequence(DicomTag.RTROIObservationsSequence,
            Observation(1, "ORGAN", new DicomDataset { { DicomTag.CodeValue, "12738006" }, { DicomTag.CodingSchemeDesignator, "SCT" }, { DicomTag.CodeMeaning, "Brain" } }),
            Observation(3, "ORGAN", new DicomDataset { { DicomTag.CodeValue, "7088" }, { DicomTag.CodeMeaning, "NoScheme" } }),
            Observation(4, "ORGAN", new DicomDataset { { DicomTag.CodeValue, "7131" }, { DicomTag.CodingSchemeDesignator, "FMA" }, { DicomTag.CodeMeaning, "NoColour" } })));
        string rt = Path.Combine(root.Path, "partial.dcm");
        new DicomFile(dataset).Save(rt);

        var reader = new TemplateMaker();
        Assert.Equal(1, reader.interpret_RT(rt));

        Assert.Equal(new[] { "Brain" }, reader.ROIs.Select(r => r.ROIName));
        Assert.Equal(new[]
        {
            "NoObservation (ROI number 2): it has no RT ROI Observations item, so it has no interpreted type or code.",
            "NoScheme (ROI number 3): its RT ROI Identification Code has no coding scheme, and ROIs without a complete code are not imported.",
            "NoColour (ROI number 4): it has no ROI Display Color.",
        }, reader.SkippedRois);
    }

    private static DicomDataset Observation(int number, string type, DicomDataset? code)
    {
        var observation = new DicomDataset
        {
            { DicomTag.ObservationNumber, number },
            { DicomTag.ReferencedROINumber, number },
            { DicomTag.RTROIInterpretedType, type },
        };
        if (code != null)
        {
            observation.Add(new DicomSequence(DicomTag.RTROIIdentificationCodeSequence, code));
        }
        return observation;
    }
}
