using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Services;
using FellowOakDicom;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>
/// End-to-end: a template folder pointing at the bundled four-slice CT sample (Visible Human, public
/// domain) produces an RT Structure Set that references every slice and carries the template's ROIs.
/// </summary>
public class DicomRunnerTests
{
    private static string CopySampleSeries(TestFolder folder)
    {
        string dicomDir = folder.Sub("PatientImages");
        foreach (string file in Directory.GetFiles(TestFolder.Data("SmallCT")))
        {
            File.Copy(file, System.IO.Path.Combine(dicomDir, System.IO.Path.GetFileName(file)));
        }

        return dicomDir;
    }

    private static void MakeTemplate(string templateRoot, string name, string dicomDir)
    {
        var maker = new TemplateMaker();
        maker.define_output(System.IO.Path.Combine(templateRoot, name));
        maker.set_onto_path(System.IO.Path.Combine(templateRoot, "Ontologies"));
        maker.Paths.Add(dicomDir);
        maker.ROIs.Add(new ROIClass(255, 0, 0, "Brain", "ORGAN", new OntologyCodeClass("Brain", "50801", "FMA")));
        maker.ROIs.Add(new ROIClass(0, 255, 0, "PTV_High", "PTV", new OntologyCodeClass("PTV_High", "PTV_High", "99VMS_STRUCTCODE")));
        maker.make_template();
    }

    [Fact]
    public void Series_discovery_groups_the_sample_ct_and_ignores_non_images()
    {
        using var folder = new TestFolder();
        string dicomDir = CopySampleSeries(folder);
        File.WriteAllText(System.IO.Path.Combine(dicomDir, "notes.txt"), "not DICOM");
        File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "template_RS.dcm"), System.IO.Path.Combine(dicomDir, "existing_RS.dcm"));

        var parser = new DicomParser();
        parser.ParseDirectory(dicomDir);

        string uid = Assert.Single(parser.dicom_series_instance_uids);
        List<string> files = parser.series_instance_uids_dict[uid];
        Assert.Equal(4, files.Count);
        List<double> z = files.Select(f => DicomFile.Open(f).Dataset.GetValues<double>(DicomTag.ImagePositionPatient)[2]).ToList();
        Assert.Equal(z.OrderBy(v => v), z);
    }

    [Fact]
    public void Runner_writes_an_rt_structure_set_referencing_every_slice()
    {
        using var folder = new TestFolder();
        string dicomDir = CopySampleSeries(folder);
        string templateRoot = folder.Sub("Templates");
        MakeTemplate(templateRoot, "Brain_Test", dicomDir);

        var runner = new DicomTemplateRunner(templateRoot);
        runner.build_dictionary();
        runner.walk_down_folders(false);

        string output = Assert.Single(Directory.GetFiles(dicomDir, "Brain_Test_UID*.dcm"));
        DicomDataset rt = DicomFile.Open(output).Dataset;
        DicomDataset firstCt = DicomFile.Open(Directory.GetFiles(dicomDir, "vhm.*.dcm").First()).Dataset;

        Assert.Equal("RTSTRUCT", rt.GetString(DicomTag.Modality));
        Assert.Equal("Brain_Test", rt.GetString(DicomTag.StructureSetLabel));
        Assert.Equal(firstCt.GetString(DicomTag.PatientID), rt.GetString(DicomTag.PatientID));
        Assert.Equal(firstCt.GetString(DicomTag.StudyInstanceUID), rt.GetString(DicomTag.StudyInstanceUID));

        List<string> roiNames = rt.GetSequence(DicomTag.StructureSetROISequence).Items.Select(i => i.GetString(DicomTag.ROIName)).ToList();
        Assert.Equal(new[] { "Brain", "PTV_High" }, roiNames);
        Assert.All(rt.GetSequence(DicomTag.StructureSetROISequence).Items,
            i => Assert.Equal(firstCt.GetString(DicomTag.FrameOfReferenceUID), i.GetString(DicomTag.ReferencedFrameOfReferenceUID)));

        DicomDataset observation = rt.GetSequence(DicomTag.RTROIObservationsSequence).Items[1];
        Assert.Equal("PTV", observation.GetString(DicomTag.RTROIInterpretedType));
        Assert.Equal("PTV_High", observation.GetSequence(DicomTag.RTROIIdentificationCodeSequence).Items[0].GetString(DicomTag.CodeValue));

        List<string> referenced = rt.GetSequence(DicomTag.ReferencedFrameOfReferenceSequence).Items[0]
            .GetSequence(DicomTag.RTReferencedStudySequence).Items[0]
            .GetSequence(DicomTag.RTReferencedSeriesSequence).Items[0]
            .GetSequence(DicomTag.ContourImageSequence).Items
            .Select(i => i.GetString(DicomTag.ReferencedSOPInstanceUID)).ToList();
        List<string> sliceUids = Directory.GetFiles(dicomDir, "vhm.*.dcm").Select(f => DicomFile.Open(f).Dataset).OrderBy(d => d.GetValues<double>(DicomTag.ImagePositionPatient)[2]).Select(d => d.GetString(DicomTag.SOPInstanceUID)).ToList();
        Assert.Equal(sliceUids, referenced);
    }

    [Fact]
    public void Runner_skips_a_folder_it_has_already_processed()
    {
        using var folder = new TestFolder();
        string dicomDir = CopySampleSeries(folder);
        string templateRoot = folder.Sub("Templates");
        MakeTemplate(templateRoot, "Brain_Test", dicomDir);
        var runner = new DicomTemplateRunner(templateRoot);
        runner.build_dictionary();
        runner.walk_down_folders(false);
        string first = Assert.Single(Directory.GetFiles(dicomDir, "Brain_Test_UID*.dcm"));
        DateTime written = File.GetLastWriteTimeUtc(first);

        runner.build_dictionary();
        runner.walk_down_folders(false);

        Assert.Equal(written, File.GetLastWriteTimeUtc(Assert.Single(Directory.GetFiles(dicomDir, "Brain_Test_UID*.dcm"))));
    }
}
