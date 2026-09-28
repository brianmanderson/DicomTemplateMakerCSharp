using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using TemplateSync.Model;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>AirTableEntry (Airtable row) to ROIClass and back, following the former add_roi and AirTableEntry(ROIClass) rules.</summary>
public class TemplateEntryMapperTests
{
    [Fact]
    public void Entry_becomes_an_roi_with_ontology_colour_dvh_and_language_variants()
    {
        var entry = new AirTableEntry
        {
            Structure = "Parotid_L",
            CommonName = "Left parotid gland",
            Type = "ORGAN",
            RGB = "10,20,30",
            SchemeCode = "59798",
            Scheme = "FMA",
            DVH_Color = "255",
            DVH_Style = "dashed",
            DVH_Width = "2",
            TG_263R = "L_Parotid",
            TG_263Spanish = "Parotida_I",
            TG_263SpanishR = "I_Parotida",
            TG_263French = "Parotide_G",
            TG_263FrenchR = "G_Parotide",
        };
        var warnings = new List<string>();

        ROIWrapper? wrapper = TemplateEntryMapper.ToRoiWrapper(new SiteRoi(entry, include: false), warnings);

        Assert.NotNull(wrapper);
        ROIClass roi = wrapper!.roi;
        Assert.Equal("Parotid_L", roi.ROIName);
        Assert.Equal((byte)10, roi.R);
        Assert.Equal((byte)30, roi.B);
        Assert.False(roi.Include);
        Assert.Equal("ORGAN", roi.ROI_Interpreted_type);
        Assert.NotNull(roi.Ontology_Class);
        Assert.Equal("Left parotid gland", roi.Ontology_Class.CodeMeaning);
        Assert.Equal("59798", roi.Ontology_Class.CodeValue);
        Assert.Equal("VMS011", roi.Ontology_Class.ContextIdentifier);
        Assert.Equal("255", roi.DVHLineColor);
        Assert.Equal((byte)255, roi.R_DVH);
        Assert.Equal("dashed", roi.DVHLineStyle);
        Assert.Equal("2", roi.DVHLineWidth);
        Assert.True(wrapper.has_lateral);
        Assert.True(wrapper.has_other_lanuages);
        wrapper.Set_Spanish(true);
        Assert.Equal("I_Parotida", roi.ROIName);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Missing_colour_keeps_the_roi_in_red_and_warns()
    {
        var warnings = new List<string>();

        ROIWrapper? wrapper = TemplateEntryMapper.ToRoiWrapper(new SiteRoi(new AirTableEntry { Structure = "Bladder" }, true), warnings);

        Assert.Equal((255, 0, 0), (wrapper!.roi.R, wrapper.roi.G, wrapper.roi.B));
        Assert.Contains(warnings, w => w.Contains("Bladder"));
    }

    [Fact]
    public void Invalid_dvh_colour_falls_back_to_the_roi_colour()
    {
        var warnings = new List<string>();

        ROIWrapper? wrapper = TemplateEntryMapper.ToRoiWrapper(new SiteRoi(new AirTableEntry { Structure = "Bladder", RGB = "1,2,3", DVH_Color = "not-a-number" }, true), warnings);

        Assert.Equal((byte)1, wrapper!.roi.R_DVH);
        Assert.Single(warnings);
    }

    [Fact]
    public void Nameless_entries_are_skipped_with_a_warning()
    {
        var warnings = new List<string>();

        Assert.Null(TemplateEntryMapper.ToRoiWrapper(new SiteRoi(new AirTableEntry { Structure = " " }, true), warnings));
        Assert.Single(warnings);
    }

    [Fact]
    public void Roi_becomes_the_entry_the_old_code_wrote()
    {
        var roi = new ROIClass(1, 2, 3, "Brain", "ORGAN", new OntologyCodeClass("Brain", "50801", "FMA")) { TypeIndex = "3", ContourStyle = "segment" };

        AirTableEntry entry = TemplateEntryMapper.ToDesiredEntry(roi);

        Assert.Equal("Brain", entry.Structure);
        Assert.Equal("Brain", entry.CommonName);
        Assert.Equal("50801", entry.SchemeCode);
        Assert.Equal("1,2,3", entry.RGB);
        Assert.Equal(new[] { "Auto:1,2,3" }, entry.Colors_RGB);
        Assert.Equal("-16777216", entry.DVH_Color);
        Assert.Equal("3", entry.DVH_Type_Index);
        Assert.Equal("segment", entry.DVH_ContourStyle);
        Assert.Empty(entry.Template_Recommend);
        Assert.Null(entry.Id);
    }

    [Fact]
    public void Ontology_defaults_match_between_the_two_libraries()
    {
        var ontology = new OntologyCodeClass();

        Assert.Equal(OntologyDefaults.Scheme, ontology.Scheme);
        Assert.Equal(OntologyDefaults.ContextGroupVersion, ontology.ContextGroupVersion);
        Assert.Equal(OntologyDefaults.MappingResource, ontology.MappingResource);
        Assert.Equal(OntologyDefaults.ContextIdentifier, ontology.ContextIdentifier);
        Assert.Equal(OntologyDefaults.MappingResourceName, ontology.MappingResourceName);
        Assert.Equal(OntologyDefaults.MappingResourceUID, ontology.MappingResourceUID);
        Assert.Equal(OntologyDefaults.ContextUID, ontology.ContextUID);
    }
}
