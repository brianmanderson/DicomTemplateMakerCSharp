using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class OntologyEntryRulesTests
{
    private readonly OntologyCodeClass heart = new("Heart", "7088", "FMA");
    private readonly OntologyCodeClass brain = new("Brain", "50801", "FMA");

    private List<OntologyCodeClass> Library() => new() { heart, brain };

    [Theory]
    [InlineData("", "123", "FMA", "common name")]
    [InlineData("??", "123", "FMA", "common name")]
    [InlineData("Liver", "", "FMA", "code value (required)")]
    [InlineData("Liver", "   ", "FMA", "code value (required)")]
    [InlineData("Liver", null, "FMA", "code value (required)")]
    [InlineData("Liver", "7197", "", "coding scheme")]
    [InlineData("Liver", "7088", "SCT", "already used by \"Heart\" (FMA)")]
    [InlineData("Liver", " 7088 ", "FMA", "already used by \"Heart\"")]
    public void An_entry_needs_a_name_a_unique_code_value_and_a_scheme(string name, string? code, string scheme, string reason)
    {
        string? problem = OntologyEntryRules.Problem(name, code, scheme, Library(), null);

        Assert.NotNull(problem);
        Assert.Contains(reason, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_valid_entry_and_an_entry_keeping_its_own_code_pass()
    {
        Assert.Null(OntologyEntryRules.Problem("Liver", "7197", "FMA", Library(), null));
        Assert.Null(OntologyEntryRules.Problem("Heart muscle", "7088", "FMA", Library(), heart));
    }

    [Fact]
    public void A_repeated_name_is_only_a_warning()
    {
        Assert.Contains("also named \"Heart\"", OntologyEntryRules.Warning("heart", Library(), null), StringComparison.Ordinal);
        Assert.Null(OntologyEntryRules.Warning("Heart", Library(), heart));
        Assert.Null(OntologyEntryRules.Warning("Liver", Library(), null));
    }

    [Fact]
    public void The_search_survives_entries_without_code_value_or_scheme()
    {
        var bare = new OntologyCodeClass { CodeMeaning = "Bare", CodeValue = null, Scheme = null };

        Assert.True(OntologyEntryRules.Matches(bare, "bar"));
        Assert.False(OntologyEntryRules.Matches(bare, "fma"));
        Assert.True(OntologyEntryRules.Matches(heart, "fma"));
        Assert.True(OntologyEntryRules.Matches(heart, "708"));
        Assert.True(OntologyEntryRules.Matches(heart, " HEART "));
        Assert.True(OntologyEntryRules.Matches(bare, ""));
    }

    [Fact]
    public void Templates_using_an_entry_are_found_by_code_value()
    {
        var a = Template("Prostate", new OntologyCodeClass("Heart", "7088", "FMA"));
        var b = Template("Breast", heart);
        var c = Template("Brain", brain);
        var d = Template("Nothing", null);

        IReadOnlyList<string> users = OntologyEntryRules.TemplatesUsing(heart, new[] { a, b, c, d });

        Assert.Equal(new[] { "Breast", "Prostate" }, users);
        Assert.Empty(OntologyEntryRules.TemplatesUsing(new OntologyCodeClass("Liver", "7197", "FMA"), new[] { a, b, c, d }));
    }

    private static TemplateMaker Template(string name, OntologyCodeClass? code)
    {
        var maker = new TemplateMaker { TemplateName = name };
        var roi = new ROIClass(1, 2, 3, name + "_roi", "ORGAN", new OntologyCodeClass("x", "x", "x")) { Ontology_Class = code };
        maker.ROIs.Add(roi);
        return maker;
    }
}
