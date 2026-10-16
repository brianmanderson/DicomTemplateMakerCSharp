using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Validation;
using FellowOakDicom;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>
/// The TPS-realism rules RR1-RR10 on RTs the builder makes from the bundled sample CT: a clean RT passes every rule,
/// and each rule catches the one edit that breaks it.
/// </summary>
public class RtRealismCheckerTests
{
    private static readonly DateTimeOffset GenerationTime = new DateTimeOffset(2031, 3, 4, 22, 30, 15, TimeSpan.Zero);

    private static OntologyCodeClass Code(string meaning, string value, string scheme) => new OntologyCodeClass(meaning, value, scheme)
    {
        ContextIdentifier = null,
        ContextGroupVersion = null,
        MappingResource = null,
        MappingResourceName = null,
        MappingResourceUID = null,
        ContextUID = null,
    };

    /// <summary>
    /// An RT of three ROIs with SNOMED CT and FMA codes and no context attributes, without the retired ROI Observation
    /// Label the builder still writes: an RT every rule accepts.
    /// </summary>
    private static DicomDataset Clean(string label = "Realism_Test")
    {
        List<DicomDataset> slices = Directory.GetFiles(TestFolder.Data("SmallCT")).Order(StringComparer.Ordinal).Select(f => DicomFile.Open(f).Dataset).ToList();
        var rois = new List<ROIClass>
        {
            new ROIClass(255, 0, 0, "Brain", "ORGAN", Code("Brain", "12738006", "SCT")),
            new ROIClass(0, 255, 0, "PTV_High", "PTV", Code("Planning target volume", "228793007", "SCT")),
            new ROIClass(0, 0, 255, "SpinalCord", "OAR", Code("Spinal cord", "7647", "FMA")),
        };
        RtBuildResult result = new RtStructureBuilder(DicomFile.Open(DicomTemplateRunner.DefaultTemplateRsPath).Dataset).Build(slices, label, rois, GenerationTime);
        DicomDataset rt = Assert.IsType<DicomFile>(result.File).Dataset;
        foreach (DicomDataset observation in Items(rt, DicomTag.RTROIObservationsSequence))
        {
            observation.Remove(DicomTag.ROIObservationLabelRETIRED);
        }

        return rt;
    }

    private static IList<DicomDataset> Items(DicomDataset dataset, DicomTag tag) => dataset.GetSequence(tag).Items;

    private static DicomDataset FirstCode(DicomDataset rt, int observation = 0) =>
        Items(Items(rt, DicomTag.RTROIObservationsSequence)[observation], DicomTag.RTROIIdentificationCodeSequence)[0].NotValidated();

    private static DicomDataset ContourImages(DicomDataset rt)
    {
        DicomDataset frame = Items(rt, DicomTag.ReferencedFrameOfReferenceSequence)[0];
        DicomDataset study = Items(frame, DicomTag.RTReferencedStudySequence)[0];
        return Items(study, DicomTag.RTReferencedSeriesSequence)[0];
    }

    private static List<RealismFinding> Of(DicomDataset rt, string rule) => RtRealismChecker.Check(rt).Where(f => f.RuleId == rule).ToList();

    [Fact]
    public void A_clean_rt_passes_every_rule()
    {
        DicomDataset rt = Clean();

        Assert.Empty(RtRealismChecker.Check(rt));
        Assert.Empty(RtRealismChecker.CheckSet(new[] { ("a.dcm", rt) }));
    }

    [Fact]
    public void RR1_an_rt_with_rois_passes()
    {
        Assert.Empty(Of(Clean(), "RR1"));
    }

    [Fact]
    public void RR1_an_empty_structure_set_roi_sequence_is_an_error()
    {
        DicomDataset rt = Clean();
        rt.AddOrUpdate(new DicomSequence(DicomTag.StructureSetROISequence));

        RealismFinding finding = Assert.Single(Of(rt, "RR1"));
        Assert.Equal(RealismSeverity.Error, finding.Severity);
    }

    [Fact]
    public void RR2_matching_roi_numbers_pass()
    {
        Assert.Empty(Of(Clean(), "RR2"));
    }

    [Fact]
    public void RR2_a_duplicate_or_dangling_roi_number_is_an_error()
    {
        DicomDataset rt = Clean();
        Items(rt, DicomTag.ROIContourSequence)[1].AddOrUpdate(DicomTag.ReferencedROINumber, 1);
        Items(rt, DicomTag.RTROIObservationsSequence)[2].AddOrUpdate(DicomTag.ReferencedROINumber, 9);

        List<RealismFinding> findings = Of(rt, "RR2");
        Assert.All(findings, f => Assert.Equal(RealismSeverity.Error, f.Severity));
        Assert.Contains(findings, f => f.Message.Contains("ROI Contour Sequence lists Referenced ROI Number 1 more than once", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Message.Contains("ROI Contour Sequence has no item for ROI number(s) 2", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Message.Contains("RT ROI Observations Sequence refers to ROI number(s) 9", StringComparison.Ordinal));
    }

    [Fact]
    public void RR3_a_contour_sequence_with_contours_or_none_at_all_passes()
    {
        DicomDataset rt = Clean();
        var contour = new DicomDataset { { DicomTag.ContourGeometricType, "POINT" }, { DicomTag.NumberOfContourPoints, 1 }, { DicomTag.ContourData, 0.0, 0.0, 0.0 } };
        Items(rt, DicomTag.ROIContourSequence)[0].AddOrUpdate(new DicomSequence(DicomTag.ContourSequence, contour));

        Assert.Empty(Of(rt, "RR3"));
    }

    [Fact]
    public void RR3_an_empty_contour_sequence_is_an_error()
    {
        DicomDataset rt = Clean();
        Items(rt, DicomTag.ROIContourSequence)[1].AddOrUpdate(new DicomSequence(DicomTag.ContourSequence));

        RealismFinding finding = Assert.Single(Of(rt, "RR3"));
        Assert.Equal(RealismSeverity.Error, finding.Severity);
        Assert.Contains("ROI 2 'PTV_High'", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RR4_one_frame_study_and_series_listing_each_image_once_pass()
    {
        DicomDataset rt = Clean();

        Assert.Empty(Of(rt, "RR4"));
        Assert.Equal(4, Items(ContourImages(rt), DicomTag.ContourImageSequence).Count);
    }

    [Fact]
    public void RR4_a_duplicate_image_or_a_second_study_is_an_error()
    {
        DicomDataset duplicate = Clean();
        IList<DicomDataset> images = Items(ContourImages(duplicate), DicomTag.ContourImageSequence);
        images.Add(images[0].Clone());
        RealismFinding finding = Assert.Single(Of(duplicate, "RR4"));
        Assert.Equal(RealismSeverity.Error, finding.Severity);
        Assert.Contains("more than once", finding.Message, StringComparison.Ordinal);

        DicomDataset twoStudies = Clean();
        IList<DicomDataset> studies = Items(Items(twoStudies, DicomTag.ReferencedFrameOfReferenceSequence)[0], DicomTag.RTReferencedStudySequence);
        studies.Add(studies[0].Clone());
        Assert.Contains("RT Referenced Study Sequence (3006,0012) has 2 item(s)", Assert.Single(Of(twoStudies, "RR4")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RR5_a_defined_term_or_an_empty_type_passes()
    {
        DicomDataset rt = Clean();
        Items(rt, DicomTag.RTROIObservationsSequence)[0].AddOrUpdate(DicomTag.RTROIInterpretedType, string.Empty);

        Assert.Empty(Of(rt, "RR5"));
    }

    [Fact]
    public void RR5_an_undefined_interpreted_type_is_an_error()
    {
        DicomDataset rt = Clean();
        Items(rt, DicomTag.RTROIObservationsSequence)[0].AddOrUpdate(DicomTag.RTROIInterpretedType, "TUMOR");

        RealismFinding finding = Assert.Single(Of(rt, "RR5"));
        Assert.Equal(RealismSeverity.Error, finding.Severity);
        Assert.Contains("'TUMOR'", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RR6_distinct_names_of_up_to_16_characters_pass()
    {
        DicomDataset rt = Clean();
        Items(rt, DicomTag.StructureSetROISequence)[2].AddOrUpdate(DicomTag.ROIName, "SpinalCord_PRV05");

        Assert.Empty(Of(rt, "RR6"));
    }

    [Fact]
    public void RR6_a_name_repeated_in_other_case_is_an_error_and_a_long_name_a_warning()
    {
        DicomDataset rt = Clean();
        Items(rt, DicomTag.StructureSetROISequence)[1].AddOrUpdate(DicomTag.ROIName, "BRAIN  ");
        Items(rt, DicomTag.StructureSetROISequence)[2].AddOrUpdate(DicomTag.ROIName, "SpinalCord_PRV05mm");

        List<RealismFinding> findings = Of(rt, "RR6");
        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Severity == RealismSeverity.Error && f.Message.Contains("'BRAIN' repeats 'Brain'", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Severity == RealismSeverity.Warning && f.Message.Contains("has 18 characters", StringComparison.Ordinal));
    }

    [Fact]
    public void RR7_distinct_labels_pass()
    {
        Assert.Empty(RtRealismChecker.CheckSet(new[] { ("a.dcm", Clean("Site_A")), ("b.dcm", Clean("Site_B")) }));
    }

    [Fact]
    public void RR7_a_shared_or_empty_label_is_an_error()
    {
        DicomDataset empty = Clean("Site_C");
        empty.AddOrUpdate(DicomTag.StructureSetLabel, string.Empty);

        var findings = RtRealismChecker.CheckSet(new[] { ("a.dcm", Clean("Site_A")), ("b.dcm", Clean("site_a")), ("c.dcm", empty) });

        Assert.All(findings, f => Assert.Equal(("RR7", RealismSeverity.Error), (f.Finding.RuleId, f.Finding.Severity)));
        Assert.Equal(new[] { "a.dcm", "b.dcm", "c.dcm" }, findings.Select(f => f.Name).Order(StringComparer.Ordinal));
        Assert.Contains("also the label of b.dcm", findings.Single(f => f.Name == "a.dcm").Finding.Message, StringComparison.Ordinal);
        Assert.Contains("is empty", findings.Single(f => f.Name == "c.dcm").Finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RR8_the_verhoeff_check_digit_of_a_snomed_identifier()
    {
        Assert.True(RtRealismChecker.IsValidVerhoeff("228793007"));
        Assert.False(RtRealismChecker.IsValidVerhoeff("228793008"));
        Assert.True(RtRealismChecker.IsValidVerhoeff("12738006"));
        Assert.False(RtRealismChecker.IsValidVerhoeff(string.Empty));
        Assert.False(RtRealismChecker.IsValidVerhoeff("2287930O7"));
    }

    [Fact]
    public void RR8_valid_snomed_and_fma_codes_pass()
    {
        DicomDataset rt = Clean();
        // Schemes other than SCT and FMA are not checked for syntax; 16 characters still fit Code Value.
        FirstCode(rt).AddOrUpdate(DicomTag.CodeValue, "ABCDEFGHIJKLMNOP");
        FirstCode(rt).AddOrUpdate(DicomTag.CodingSchemeDesignator, "99LOCAL");

        Assert.Empty(Of(rt, "RR8"));
        Assert.Empty(Of(Clean(), "RR8"));
    }

    [Theory]
    [InlineData("SCT", "88", "is not 6 to 18 digits")]
    [InlineData("SCT", "228793008", "fails the Verhoeff check digit")]
    [InlineData("SCT", "1234070", "has partition identifier 07")]
    [InlineData("FMA", "FMA7647", "is not an integer")]
    [InlineData("99LOCAL", "ABCDEFGHIJKLMNOPQ", "has 17 characters")]
    public void RR8_a_code_value_with_the_wrong_syntax_is_an_error(string scheme, string value, string expected)
    {
        DicomDataset rt = Clean();
        FirstCode(rt).AddOrUpdate(DicomTag.CodingSchemeDesignator, scheme);
        FirstCode(rt).AddOrUpdate(DicomTag.CodeValue, value);

        RealismFinding finding = Assert.Single(Of(rt, "RR8"));
        Assert.Equal(RealismSeverity.Error, finding.Severity);
        Assert.Contains("ROI 'Brain'", finding.Message, StringComparison.Ordinal);
        Assert.Contains(expected, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RR9_a_complete_context_on_a_varian_code_passes()
    {
        DicomDataset rt = Clean();
        DicomDataset code = FirstCode(rt);
        code.AddOrUpdate(DicomTag.CodingSchemeDesignator, "99VMS_STRUCTCODE");
        code.AddOrUpdate(DicomTag.CodeValue, "Brain");
        code.AddOrUpdate(DicomTag.ContextIdentifier, "VMS011");
        code.AddOrUpdate(DicomTag.MappingResource, "99VMS");
        code.AddOrUpdate(DicomTag.ContextGroupVersion, "20161209");
        code.AddOrUpdate(DicomTag.MappingResourceUID, "1.2.246.352.7.1.1");
        code.AddOrUpdate(DicomTag.MappingResourceName, "Varian Medical Systems");
        code.AddOrUpdate(DicomTag.ContextUID, "1.2.246.352.7.2.11");

        Assert.Empty(Of(rt, "RR9"));
    }

    [Fact]
    public void RR9_a_partial_context_varian_context_on_snomed_and_the_retired_label_are_warnings()
    {
        DicomDataset rt = Clean();
        DicomDataset brain = FirstCode(rt, 0);
        brain.AddOrUpdate(DicomTag.ContextUID, "1.2.246.352.7.2.11");
        brain.AddOrUpdate(DicomTag.MappingResourceName, "Varian Medical Systems");
        Items(rt, DicomTag.RTROIObservationsSequence)[1].AddOrUpdate(DicomTag.ROIObservationLabelRETIRED, "PTV_High");

        List<RealismFinding> findings = Of(rt, "RR9");
        Assert.Equal(3, findings.Count);
        Assert.All(findings, f => Assert.Equal(RealismSeverity.Warning, f.Severity));
        Assert.Contains(findings, f => f.Message.StartsWith("ROI 'Brain': the code has Mapping Resource Name, Context UID but not Context Identifier", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Message.StartsWith("Codes of scheme SCT carry Varian context attributes (Context UID 1.2.246.352.7.2.11, Mapping Resource Name Varian Medical Systems)", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Message.Contains("present in some RT ROI Observations items", StringComparison.Ordinal));
    }

    [Fact]
    public void RR10_one_frame_of_reference_and_an_empty_position_reference_indicator_pass()
    {
        DicomDataset rt = Clean();

        Assert.Equal(string.Empty, rt.GetString(DicomTag.PositionReferenceIndicator));
        Assert.Empty(Of(rt, "RR10"));
    }

    [Fact]
    public void RR10_a_missing_position_reference_indicator_or_another_frame_of_reference_is_an_error()
    {
        DicomDataset rt = Clean();
        rt.Remove(DicomTag.PositionReferenceIndicator);
        Items(rt, DicomTag.StructureSetROISequence)[2].AddOrUpdate(DicomTag.ReferencedFrameOfReferenceUID, "1.2.3.4");

        List<RealismFinding> findings = Of(rt, "RR10");
        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal(RealismSeverity.Error, f.Severity));
        Assert.Contains(findings, f => f.Message.StartsWith("Position Reference Indicator (0020,1040) is missing", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Message.StartsWith("Structure Set ROI 'SpinalCord' has Referenced Frame of Reference UID 1.2.3.4", StringComparison.Ordinal));

        rt.Remove(DicomTag.FrameOfReferenceUID);
        Assert.Contains(Of(rt, "RR10"), f => f.Message.StartsWith("Frame of Reference UID (0020,0052) is missing", StringComparison.Ordinal));
    }
}
