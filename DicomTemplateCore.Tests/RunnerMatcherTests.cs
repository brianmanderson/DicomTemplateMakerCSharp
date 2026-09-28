using DicomTemplateMakerGUI.DicomTemplateServices;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>
/// <see cref="TemplateMatcher"/>: the old two-way, case-insensitive substring rule, except that blank or missing
/// values never match and blank requirements are ignored.
/// </summary>
public class RunnerMatcherTests
{
    private static TemplateRequirements Series(params string[] values) => new TemplateRequirements(values, Array.Empty<string>());

    private static TemplateRequirements Study(params string[] values) => new TemplateRequirements(Array.Empty<string>(), values);

    [Theory]
    [InlineData("Brain", "brain mri t1")] // description contains the requirement
    [InlineData("BRAIN MRI T1 AXIAL", "Brain MRI")] // requirement contains the description
    [InlineData("head", "HEAD")] // equal ignoring case
    public void Substring_in_either_direction_ignoring_case_matches(string requirement, string description)
    {
        Assert.True(TemplateMatcher.Matches(Series(requirement), description, null));
        Assert.True(TemplateMatcher.Matches(Study(requirement), null, description));
    }

    [Theory]
    [InlineData("Prostate T2 AX", "T2")]
    [InlineData("CT Pelvis", "CT")]
    [InlineData("H&N Planning CT", "H&N")]
    public void A_short_description_contained_in_a_requirement_still_matches(string requirement, string description)
    {
        // Kept on purpose: existing templates rely on the two-way rule. Narrowing it (e.g. whole words only) would stop
        // some templates producing RTs without any message, so it is left to the maintainers (docs/MODERNIZATION_PLAN.md,
        // open question 5).
        Assert.True(TemplateMatcher.Matches(Series(requirement), description, null));
    }

    [Theory]
    [InlineData("Brain", "Pelvis")]
    [InlineData("Head and Neck", "Neck Head")]
    public void Unrelated_descriptions_do_not_match(string requirement, string description)
    {
        Assert.False(TemplateMatcher.Matches(Series(requirement), description, description));
    }

    [Fact]
    public void A_series_matches_on_series_description_or_on_study_description()
    {
        var requirements = new TemplateRequirements(new[] { "CT_Sim" }, new[] { "Prostate" });

        Assert.True(TemplateMatcher.Matches(requirements, "CT_Sim 2mm", "Unrelated"));
        Assert.True(TemplateMatcher.Matches(requirements, "Unrelated", "Prostate IMRT"));
        Assert.False(TemplateMatcher.Matches(requirements, "Unrelated", "Unrelated"));
        // Each list is compared with its own description only.
        Assert.False(TemplateMatcher.Matches(requirements, "Prostate", "CT_Sim"));
    }

    [Fact]
    public void Any_requirement_in_a_list_is_enough()
    {
        Assert.True(TemplateMatcher.Matches(Series("Pelvis", "Brain", "Lung"), "Brain w/o", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NA")] // what the old runner substituted for a missing tag
    [InlineData("na")]
    public void Blank_or_missing_descriptions_never_match_a_requirement(string? description)
    {
        // The old test matched "" against everything, and "NA" against every requirement containing "na" (Spinal, ...).
        Assert.False(TemplateMatcher.Matches(Series("Brain", "Spinal cord"), description, description));
        Assert.False(TemplateMatcher.Matches(Study("Brain", "Spinal cord"), description, description));
        Assert.False(TemplateMatcher.ValueMatches("Spinal", description));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("Brain", "Pelvis")]
    public void A_template_without_requirements_matches_every_series(string? series, string? study)
    {
        Assert.True(TemplateMatcher.Matches(TemplateRequirements.None, series, study));
        Assert.True(TemplateMatcher.Matches(new TemplateRequirements(Array.Empty<string>(), Array.Empty<string>()), series, study));
    }

    [Fact]
    public void Blank_requirements_are_ignored()
    {
        var onlyBlank = new TemplateRequirements(new[] { "", "  " }, new string?[] { null });
        Assert.False(onlyBlank.HasAny);
        Assert.True(TemplateMatcher.Matches(onlyBlank, "Anything", null));

        // A blank entry next to a real one no longer matches every series.
        var mixed = new TemplateRequirements(new[] { "", "Brain" }, Array.Empty<string>());
        Assert.Equal(new[] { "Brain" }, mixed.SeriesDescriptions);
        Assert.False(TemplateMatcher.Matches(mixed, "Pelvis", "Pelvis"));
    }

    [Fact]
    public void Parse_reads_dicom_tags_lines_and_drops_blank_values()
    {
        TemplateRequirements parsed = TemplateRequirements.Parse(new[]
        {
            "Series Description\\CT_Sim\\\\Brain",
            "Study Description\\",
            "Other Tag\\Ignored",
            "series description\\WrongCase",
            string.Empty,
        });

        Assert.Equal(new[] { "CT_Sim", "Brain" }, parsed.SeriesDescriptions);
        Assert.Empty(parsed.StudyDescriptions);
        Assert.True(parsed.HasAny);
        Assert.False(TemplateRequirements.Parse(new[] { "Series Description\\", "Study Description\\" }).HasAny);
    }
}
