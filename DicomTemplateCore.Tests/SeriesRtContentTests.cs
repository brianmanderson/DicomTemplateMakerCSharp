using DicomTemplateMakerGUI.DicomTemplateServices;
using FellowOakDicom;
using FellowOakDicom.IO.Buffer;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>What goes into each generated RT: attributes from its own images only, fresh UIDs, correct references.</summary>
public class SeriesRtContentTests
{
    private static readonly DateTimeOffset GenerationTime = new DateTimeOffset(2031, 3, 4, 22, 30, 15, TimeSpan.Zero);

    /// <summary>Series A carries full patient data and de-identification tags; series B lacks them. Both in one folder.</summary>
    private static (string Images, WrittenSeries A, WrittenSeries B, RunReport Report) RunTwoPatients(TestFolder folder)
    {
        string images = folder.Sub("Images");
        WrittenSeries a = SeriesFactory.Write(images, "a", dataset =>
        {
            dataset.AddOrUpdate(DicomTag.PatientName, "Alpha^Anna");
            dataset.AddOrUpdate(DicomTag.PatientBirthDate, "19500102");
            dataset.AddOrUpdate(DicomTag.PatientIdentityRemoved, "YES");
            dataset.AddOrUpdate(DicomTag.DeidentificationMethod, "Site pseudonymisation v2");
            dataset.AddOrUpdate(new DicomSequence(DicomTag.DeidentificationMethodCodeSequence, new DicomDataset
            {
                { DicomTag.CodeValue, "113100" },
                { DicomTag.CodingSchemeDesignator, "DCM" },
                { DicomTag.CodeMeaning, "Basic Application Confidentiality Profile" },
            }));
        }, seriesUid: "1.2.826.0.1.3680043.2.1125.1");
        WrittenSeries b = SeriesFactory.Write(images, "b", dataset =>
        {
            foreach (DicomTag tag in new[]
            {
                DicomTag.PatientName, DicomTag.PatientBirthDate, DicomTag.PatientSex, DicomTag.StudyDate, DicomTag.StudyTime,
                DicomTag.AccessionNumber, DicomTag.ReferringPhysicianName, DicomTag.StudyID, DicomTag.StudyDescription,
                DicomTag.PatientIdentityRemoved, DicomTag.DeidentificationMethod, DicomTag.DeidentificationMethodCodeSequence,
            })
            {
                dataset.Remove(tag);
            }

            dataset.AddOrUpdate(DicomTag.PatientID, "B-2");
        }, seriesUid: "1.2.826.0.1.3680043.2.1125.2");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });
        RunReport report = RunnerTemplates.Runner(templates, new RunnerClock(GenerationTime)).RunOnce(TestContext.Current.CancellationToken);
        return (images, a, b, report);
    }

    [Fact]
    public void Attributes_the_images_lack_are_empty_or_absent_not_the_previous_series_or_template_values()
    {
        // The old runner reused one RT object: series B kept series A's patient, or the template's "RS_Template_File".
        using var folder = new TestFolder();
        (string images, WrittenSeries a, WrittenSeries b, RunReport report) = RunTwoPatients(folder);
        Assert.Empty(report.Errors);

        DicomDataset rtA = RunnerTemplates.ReadOutput(images, "Brain_Test", a.SeriesInstanceUid).Dataset;
        Assert.Equal("Alpha^Anna", rtA.GetString(DicomTag.PatientName));
        Assert.Equal("19500102", rtA.GetString(DicomTag.PatientBirthDate));
        Assert.Equal("Visible Human Male", rtA.GetString(DicomTag.StudyDescription));

        DicomDataset rtB = RunnerTemplates.ReadOutput(images, "Brain_Test", b.SeriesInstanceUid).Dataset;
        Assert.Equal("B-2", rtB.GetString(DicomTag.PatientID));
        foreach (DicomTag type2 in new[]
        {
            DicomTag.PatientName, DicomTag.PatientBirthDate, DicomTag.PatientSex, DicomTag.StudyDate, DicomTag.StudyTime,
            DicomTag.AccessionNumber, DicomTag.ReferringPhysicianName, DicomTag.StudyID,
        })
        {
            Assert.True(rtB.Contains(type2), $"{type2} must be present (Type 2)");
            Assert.Equal(string.Empty, rtB.GetString(type2));
        }

        Assert.False(rtB.Contains(DicomTag.StudyDescription));
        Assert.Equal(b.StudyInstanceUid, rtB.GetString(DicomTag.StudyInstanceUID));
    }

    [Fact]
    public void Attributes_do_not_leak_between_folders_of_one_run()
    {
        // Measured on the old runner: B's RT carried A's patient name.
        using var folder = new TestFolder();
        string imagesA = folder.Sub("A");
        string imagesB = folder.Sub("B");
        SeriesFactory.Write(imagesA, "a", dataset => dataset.AddOrUpdate(DicomTag.PatientName, "Alpha^Anna"));
        WrittenSeries b = SeriesFactory.Write(imagesB, "b", dataset =>
        {
            dataset.Remove(DicomTag.PatientName);
            dataset.Remove(DicomTag.PatientBirthDate);
        });
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { imagesA, imagesB });

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Equal(2, report.WrittenCount);
        DicomDataset rtB = RunnerTemplates.ReadOutput(imagesB, "Brain_Test", b.SeriesInstanceUid).Dataset;
        Assert.Equal(string.Empty, rtB.GetString(DicomTag.PatientName));
        Assert.Equal(string.Empty, rtB.GetString(DicomTag.PatientBirthDate));
    }

    [Fact]
    public void De_identification_tags_follow_the_images()
    {
        // Every RT used to inherit the template's PatientIdentityRemoved=YES and RaySearch method.
        using var folder = new TestFolder();
        (string images, WrittenSeries a, WrittenSeries b, _) = RunTwoPatients(folder);

        DicomDataset rtA = RunnerTemplates.ReadOutput(images, "Brain_Test", a.SeriesInstanceUid).Dataset;
        Assert.Equal("YES", rtA.GetString(DicomTag.PatientIdentityRemoved));
        Assert.Equal("Site pseudonymisation v2", rtA.GetString(DicomTag.DeidentificationMethod));
        Assert.Equal("113100", rtA.GetSequence(DicomTag.DeidentificationMethodCodeSequence).Items.Single().GetString(DicomTag.CodeValue));

        DicomDataset rtB = RunnerTemplates.ReadOutput(images, "Brain_Test", b.SeriesInstanceUid).Dataset;
        Assert.False(rtB.Contains(DicomTag.PatientIdentityRemoved));
        Assert.False(rtB.Contains(DicomTag.DeidentificationMethod));
        Assert.False(rtB.Contains(DicomTag.DeidentificationMethodCodeSequence));
    }

    [Fact]
    public void Creation_series_and_structure_set_dates_are_the_local_generation_time()
    {
        // 22:30:15 UTC is 00:30:15 the next day in the clock's UTC+2 zone.
        using var folder = new TestFolder();
        (string images, WrittenSeries a, _, _) = RunTwoPatients(folder);

        DicomDataset rt = RunnerTemplates.ReadOutput(images, "Brain_Test", a.SeriesInstanceUid).Dataset;
        foreach ((DicomTag date, DicomTag time) in new[]
        {
            (DicomTag.InstanceCreationDate, DicomTag.InstanceCreationTime),
            (DicomTag.StructureSetDate, DicomTag.StructureSetTime),
            (DicomTag.SeriesDate, DicomTag.SeriesTime),
        })
        {
            Assert.Equal("20310305", rt.GetString(date));
            Assert.Equal("003015", rt.GetString(time));
        }
    }

    [Fact]
    public void The_referenced_study_is_the_image_study_and_each_rt_has_its_own_file_meta_uid()
    {
        using var folder = new TestFolder();
        (string images, WrittenSeries a, WrittenSeries b, _) = RunTwoPatients(folder);
        DicomFile fileA = RunnerTemplates.ReadOutput(images, "Brain_Test", a.SeriesInstanceUid);
        DicomFile fileB = RunnerTemplates.ReadOutput(images, "Brain_Test", b.SeriesInstanceUid);

        foreach ((DicomFile file, WrittenSeries series) in new[] { (fileA, a), (fileB, b) })
        {
            DicomDataset study = file.Dataset.GetSequence(DicomTag.ReferencedFrameOfReferenceSequence).Items.Single()
                .GetSequence(DicomTag.RTReferencedStudySequence).Items.Single();
            // It used to hold the first image's SOP Class and SOP Instance UIDs.
            Assert.Equal("1.2.840.10008.3.1.2.3.1", study.GetString(DicomTag.ReferencedSOPClassUID));
            Assert.Equal(series.StudyInstanceUid, study.GetString(DicomTag.ReferencedSOPInstanceUID));
            Assert.Equal(series.SeriesInstanceUid, study.GetSequence(DicomTag.RTReferencedSeriesSequence).Items.Single().GetString(DicomTag.SeriesInstanceUID));

            // fo-dicom does not update the File Meta when the dataset's SOP Instance UID changes; every RT carried the template's.
            Assert.Equal(file.Dataset.GetString(DicomTag.SOPInstanceUID), file.FileMetaInfo.MediaStorageSOPInstanceUID.UID);
            Assert.NotEqual("1.2.752.243.1.1.20190411110641821.1180.44535", file.FileMetaInfo.MediaStorageSOPInstanceUID.UID);
        }

        Assert.NotEqual(fileA.FileMetaInfo.MediaStorageSOPInstanceUID.UID, fileB.FileMetaInfo.MediaStorageSOPInstanceUID.UID);
        Assert.NotEqual(fileA.Dataset.GetString(DicomTag.SeriesInstanceUID), fileB.Dataset.GetString(DicomTag.SeriesInstanceUID));
    }

    [Fact]
    public void The_template_rt_file_is_not_modified()
    {
        using var folder = new TestFolder();
        byte[] before = File.ReadAllBytes(DicomTemplateRunner.DefaultTemplateRsPath);

        RunTwoPatients(folder);

        Assert.Equal(before, File.ReadAllBytes(DicomTemplateRunner.DefaultTemplateRsPath));
    }

    [Fact]
    public void Patient_names_in_another_character_set_are_kept()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct", dataset =>
        {
            dataset.AddOrUpdate(DicomTag.SpecificCharacterSet, "ISO_IR 192");
            dataset.AddOrUpdate(DicomTag.PatientName, "Петров^Иван");
        });
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        DicomDataset rt = RunnerTemplates.ReadOutput(images, "Brain_Test", series.SeriesInstanceUid).Dataset;
        Assert.Equal("ISO_IR 192", rt.GetString(DicomTag.SpecificCharacterSet));
        Assert.Equal("Петров^Иван", rt.GetString(DicomTag.PatientName));
    }

    [Fact]
    public void Non_conformant_patient_values_are_copied_as_the_images_hold_them()
    {
        // fo-dicom rejects these on AddOrUpdate; the old runner then lost the whole path without a word.
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct", dataset =>
        {
            dataset.NotValidated();
            dataset.AddOrUpdate(DicomTag.PatientSex, "Male");
            dataset.AddOrUpdate(DicomTag.StudyDate, "2005.01.01");
        });
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.Errors);
        DicomDataset rt = RunnerTemplates.ReadOutput(images, "Brain_Test", series.SeriesInstanceUid).Dataset;
        Assert.Equal("Male", rt.GetString(DicomTag.PatientSex));
        Assert.Equal("2005.01.01", rt.GetString(DicomTag.StudyDate));
    }

    [Fact]
    public void A_series_without_a_frame_of_reference_gets_no_rt_and_an_error()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct", dataset => dataset.Remove(DicomTag.FrameOfReferenceUID));
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        RunError error = Assert.Single(report.Errors);
        Assert.Equal(series.SeriesInstanceUid, error.SeriesInstanceUid);
        Assert.Contains("Frame of Reference", error.Message);
        Assert.Empty(Directory.GetFiles(images, "Brain_Test_UID*"));
    }

    [Fact]
    public void An_invalid_roi_is_reported_and_left_out_whole_while_the_others_are_written()
    {
        // The old add_roi added the structure set and contour items, then threw on the null interpreted type:
        // the RT held an ROI without observation, and the failure was swallowed.
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct");
        string templates = folder.Sub("Templates");
        string tooLong = new string('L', 70); // passes validation, then fo-dicom rejects it (LO holds 64) mid-build
        var rois = new List<ROIClass>
        {
            new ROIClass(255, 0, 0, "Brain", "organ", new OntologyCodeClass("Brain", "50801", "FMA")),
            new ROIClass(0, 0, 255, "NoType", null, new OntologyCodeClass("Lung", "7195", "FMA")),
            new ROIClass(0, 0, 255, tooLong, "ORGAN", new OntologyCodeClass("Liver", "7197", "FMA")),
            new ROIClass(0, 255, 0, "PTV_High", "PTV", new OntologyCodeClass("PTV_High", "PTV_High", "99VMS_STRUCTCODE")),
        };
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images }, rois);

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.Errors);
        Assert.Equal(new[] { "NoType", tooLong }, report.RoiFailures.Select(f => f.RoiName));
        Assert.Contains("interpreted type", report.RoiFailures[0].Reason);
        Assert.Contains("64", report.RoiFailures[1].Reason);
        Assert.All(report.RoiFailures, f => Assert.Equal(series.SeriesInstanceUid, f.SeriesInstanceUid));
        Assert.All(report.RoiFailures, f => Assert.Equal("Brain_Test", f.TemplateName));
        Assert.Equal(2, Assert.Single(report.Written).RoiCount);

        DicomDataset rt = RunnerTemplates.ReadOutput(images, "Brain_Test", series.SeriesInstanceUid).Dataset;
        AssertOnlyRois(rt, "Brain", "PTV_High");
        Assert.Equal(new[] { "ORGAN", "PTV" }, rt.GetSequence(DicomTag.RTROIObservationsSequence).Items.Select(i => i.GetString(DicomTag.RTROIInterpretedType)));
    }

    [Fact]
    public void An_roi_without_an_ontology_code_is_left_out_whole()
    {
        // Built directly: the template loader cannot hand such an ROI to the runner in every position today.
        DicomDataset template = DicomFile.Open(DicomTemplateRunner.DefaultTemplateRsPath).Dataset;
        List<DicomDataset> slices = Directory.GetFiles(TestFolder.Data("SmallCT")).Order(StringComparer.Ordinal)
            .Select(f => DicomFile.Open(f).Dataset).ToList();
        var rois = new List<ROIClass>
        {
            new ROIClass(255, 0, 0, "Brain", "ORGAN", new OntologyCodeClass("Brain", "50801", "FMA")),
            new ROIClass(0, 255, 0, "NoCode", "ORGAN", new OntologyCodeClass("x", "1", "FMA")) { Ontology_Class = null },
            new ROIClass(0, 0, 255, "  ", "ORGAN", new OntologyCodeClass("Liver", "7197", "FMA")),
        };

        RtBuildResult result = new RtStructureBuilder(template).Build(slices, "Brain_Test", rois, GenerationTime);

        Assert.Null(result.Error);
        Assert.Equal(1, result.RoiCount);
        Assert.Equal(new[] { "NoCode", "(unnamed)" }, result.RoiFailures.Select(f => f.RoiName));
        Assert.Contains("ontology", result.RoiFailures[0].Reason);
        Assert.Contains("no name", result.RoiFailures[1].Reason);
        AssertOnlyRois(Assert.IsType<DicomFile>(result.File).Dataset, "Brain");
        // The template dataset itself still holds its own placeholder ROI.
        Assert.Equal("test", template.GetSequence(DicomTag.StructureSetROISequence).Items.Single().GetString(DicomTag.ROIName));
    }

    [Theory]
    [InlineData("PatientID", "Patient ID")]
    [InlineData("StudyInstanceUID", "Study Instance UID")]
    [InlineData("FrameOfReferenceUID", "Frame of Reference UID")]
    public void A_series_whose_images_belong_to_another_patient_study_or_frame_of_reference_gets_no_rt(string tagName, string shownName)
    {
        // One Series Instance UID over an original and a pseudonymised copy: the RT would carry one patient's identity
        // and frame of reference but reference the other copy's images.
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        DicomTag tag = DicomDictionary.Default[tagName];
        WrittenSeries original = SeriesFactory.Write(images, "a");
        SeriesFactory.Write(images, "b", dataset =>
        {
            dataset.AddOrUpdate(DicomTag.FrameOfReferenceUID, original.FrameOfReferenceUid);
            dataset.AddOrUpdate(tag, tag == DicomTag.PatientID ? "OTHER-PATIENT" : SeriesFactory.NewUid());
        }, seriesUid: original.SeriesInstanceUid, studyUid: original.StudyInstanceUid);
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.Written);
        RunError error = Assert.Single(report.Errors);
        Assert.Equal(original.SeriesInstanceUid, error.SeriesInstanceUid);
        Assert.Contains("do not all have the same " + shownName, error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(images, "Brain_Test_UID*"));
    }

    [Fact]
    public void An_image_saved_twice_is_referenced_once()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct");
        File.Copy(series.Files[0], Path.Combine(images, "resent_ct1.dcm"));
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Single(report.Written);
        List<string> references = ContourImageInstances(RunnerTemplates.ReadOutput(images, "Brain_Test", series.SeriesInstanceUid).Dataset);
        Assert.Equal(4, references.Count);
        Assert.Equal(4, references.Distinct().Count());
    }

    [Fact]
    public void Undeclared_eight_bit_patient_names_are_copied_byte_for_byte_and_declared_as_latin_1()
    {
        // "Müller^Jörg" in Latin-1, in an image without (0008,0005): fo-dicom reads it as ASCII and would write "M?ller^J?rg".
        byte[] latin1 = Convert.FromHexString("4DFC6C6C65725E4AF67267");
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct", dataset =>
        {
            dataset.Remove(DicomTag.SpecificCharacterSet);
            dataset.AddOrUpdate(new DicomPersonName(DicomTag.PatientName, new[] { DicomEncoding.Default }, new MemoryByteBuffer(latin1)));
        });
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Single(report.Written);
        DicomDataset rt = RunnerTemplates.ReadOutput(images, "Brain_Test", series.SeriesInstanceUid).Dataset;
        Assert.Equal("ISO_IR 100", rt.GetString(DicomTag.SpecificCharacterSet));
        byte[] written = rt.GetDicomItem<DicomElement>(DicomTag.PatientName).Buffer.Data;
        Assert.Equal(Convert.ToHexString(latin1) + "20", Convert.ToHexString(written)); // padded to an even length
        Assert.Equal("Müller^Jörg", rt.GetString(DicomTag.PatientName));
    }

    [Fact]
    public void An_roi_name_the_images_character_set_cannot_hold_is_reported_and_left_out()
    {
        // Written into an ISO_IR 144 (Cyrillic) RT, "Hüftkopf_L" silently became "Huftkopf_L".
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct", dataset =>
        {
            dataset.AddOrUpdate(DicomTag.SpecificCharacterSet, "ISO_IR 144");
            dataset.AddOrUpdate(DicomTag.PatientName, "Петров^Иван");
        });
        string templates = folder.Sub("Templates");
        var rois = new List<ROIClass>
        {
            new ROIClass(255, 0, 0, "Brain", "ORGAN", new OntologyCodeClass("Brain", "50801", "FMA")),
            new ROIClass(0, 255, 0, "Hüftkopf_L", "ORGAN", new OntologyCodeClass("Femoral head", "16587", "FMA")),
        };
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images }, rois);

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        RoiFailure failure = Assert.Single(report.RoiFailures);
        Assert.Equal("Hüftkopf_L", failure.RoiName);
        Assert.Contains("ISO_IR 144", failure.Reason, StringComparison.Ordinal);
        DicomDataset rt = RunnerTemplates.ReadOutput(images, "Brain_Test", series.SeriesInstanceUid).Dataset;
        AssertOnlyRois(rt, "Brain");
        Assert.Equal("Петров^Иван", rt.GetString(DicomTag.PatientName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ISO_IR 100")]
    [InlineData("ISO_IR 192")]
    public void An_roi_from_the_old_bundled_files_is_written_with_a_caret(string? characterSet)
    {
        // The bundled AbdPelv_Anal and _Targets_Detailed had CTV_High^LN_3cm+ with U+02C6 (not '^') in the file name
        // and the Windows-1252 byte 0x88 in the code meaning, which reads as U+FFFD. Template folders made from them
        // still hold that; neither character is in Latin-1, so the ROI was refused for most CT and MR.
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct", dataset =>
        {
            if (characterSet == null)
            {
                dataset.Remove(DicomTag.SpecificCharacterSet);
            }
            else
            {
                dataset.AddOrUpdate(DicomTag.SpecificCharacterSet, characterSet);
            }
        });
        string templates = folder.Sub("Templates");
        string template = Path.Combine(templates, "AbdPelv_Anal");
        Directory.CreateDirectory(Path.Combine(template, "ROIs"));
        File.WriteAllBytes(
            Path.Combine(template, "ROIs", "CTV_High\u02C6LN_3cm+.txt"),
            System.Text.Encoding.Latin1.GetBytes("255\\165\\0\nCTV_High\u0088LN_3cm+\\CTV_High\\99VMS_STRUCTCODE\\20161209\\99VMS\\VMS011\\Varian Medical Systems\\1.2.246.352.7.1.1\\1.2.246.352.7.2.11\nCTV"));
        File.WriteAllLines(Path.Combine(template, "Paths.txt"), new[] { images });

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.RoiFailures);
        Assert.Single(report.Written);
        DicomDataset rt = RunnerTemplates.ReadOutput(images, "AbdPelv_Anal", series.SeriesInstanceUid).Dataset;
        AssertOnlyRois(rt, "CTV_High^LN_3cm+");
        DicomDataset code = rt.GetSequence(DicomTag.RTROIObservationsSequence).Items[0].GetSequence(DicomTag.RTROIIdentificationCodeSequence).Items[0];
        Assert.Equal("CTV_High^LN_3cm+", code.GetString(DicomTag.CodeMeaning));
        Assert.Equal("CTV_High", code.GetString(DicomTag.CodeValue));
    }

    [Theory]
    [InlineData("CTV_High\uFFFDLN_3cm+", "CTV_High\u02C6LN_3cm+", "CTV_High^LN_3cm+")]
    [InlineData("H\uFFFDftkopf_L", "H\u00FCftkopf_L", "H\u00FCftkopf_L")]
    [InlineData("CTV\u02C6Boost", "CTV_Boost", "CTV^Boost")]
    [InlineData("Femoral head", "H\u00FCftkopf_L", "Femoral head")]
    [InlineData("Br\uFFFDin", "Brain", "Br\uFFFDin")] // an ASCII letter is never lost in a Windows-1252 file
    [InlineData("H\uFFFDftkopf", "H\u00FCftkopf_L", "H\uFFFDftkopf")] // not the ROI name
    public void A_code_meaning_is_written_with_the_letters_a_legacy_file_lost(string codeMeaning, string roiName, string expected)
    {
        Assert.Equal(expected, RtStructureBuilder.RtCodeMeaning(codeMeaning, roiName));
    }

    [Theory]
    [InlineData(null, "FMA", "code value")]
    [InlineData("50801", null, "coding scheme")]
    [InlineData("  ", "  ", "code value and no coding scheme")]
    public void An_roi_whose_code_lacks_a_value_or_scheme_is_left_out_whole(string? codeValue, string? scheme, string expected)
    {
        DicomDataset template = DicomFile.Open(DicomTemplateRunner.DefaultTemplateRsPath).Dataset;
        List<DicomDataset> slices = Directory.GetFiles(TestFolder.Data("SmallCT")).Order(StringComparer.Ordinal)
            .Select(f => DicomFile.Open(f).Dataset).ToList();
        var rois = new List<ROIClass>
        {
            new ROIClass(255, 0, 0, "Brain", "ORGAN", new OntologyCodeClass("Brain", "50801", "FMA")),
            new ROIClass(0, 255, 0, "Incomplete", "ORGAN", new OntologyCodeClass("Incomplete", codeValue, scheme, null, null, null, null, null, null)),
        };

        RtBuildResult result = new RtStructureBuilder(template).Build(slices, "Brain_Test", rois, GenerationTime);

        (string name, string reason) = Assert.Single(result.RoiFailures);
        Assert.Equal("Incomplete", name);
        Assert.Contains(expected, reason, StringComparison.Ordinal);
        AssertOnlyRois(Assert.IsType<DicomFile>(result.File).Dataset, "Brain");
    }

    [Fact]
    public void Optional_code_attributes_without_a_value_are_left_out_of_the_code_item()
    {
        DicomDataset template = DicomFile.Open(DicomTemplateRunner.DefaultTemplateRsPath).Dataset;
        List<DicomDataset> slices = Directory.GetFiles(TestFolder.Data("SmallCT")).Order(StringComparer.Ordinal)
            .Select(f => DicomFile.Open(f).Dataset).ToList();
        var rois = new List<ROIClass>
        {
            new ROIClass(255, 0, 0, "Brain", "ORGAN", new OntologyCodeClass("Brain", "12738006", "SCT", null, "99VMS", null, null, null, null)),
            new ROIClass(0, 255, 0, "PTV_High", "PTV", new OntologyCodeClass("PTV_High", "PTV_High", "99VMS_STRUCTCODE")),
        };

        RtBuildResult result = new RtStructureBuilder(template).Build(slices, "Brain_Test", rois, GenerationTime);

        List<DicomDataset> codes = Assert.IsType<DicomFile>(result.File).Dataset.GetSequence(DicomTag.RTROIObservationsSequence).Items
            .Select(o => o.GetSequence(DicomTag.RTROIIdentificationCodeSequence).Items.Single()).ToList();
        DicomDataset bare = codes[0];
        Assert.Equal(new[] { "12738006", "SCT", "Brain" }, new[] { bare.GetString(DicomTag.CodeValue), bare.GetString(DicomTag.CodingSchemeDesignator), bare.GetString(DicomTag.CodeMeaning) });
        foreach (DicomTag optional in new[] { DicomTag.ContextIdentifier, DicomTag.ContextGroupVersion, DicomTag.MappingResource, DicomTag.ContextUID, DicomTag.MappingResourceName, DicomTag.MappingResourceUID })
        {
            Assert.False(bare.Contains(optional), $"{optional} must be left out when it has no value (or no Context Identifier)");
        }

        // A full Varian code keeps every attribute.
        DicomDataset full = codes[1];
        Assert.Equal("VMS011", full.GetString(DicomTag.ContextIdentifier));
        Assert.Equal("20161209", full.GetString(DicomTag.ContextGroupVersion));
        Assert.Equal("99VMS", full.GetString(DicomTag.MappingResource));
        Assert.Equal("1.2.246.352.7.2.11", full.GetString(DicomTag.ContextUID));
    }

    private static List<string> ContourImageInstances(DicomDataset rt)
    {
        return rt.GetSequence(DicomTag.ReferencedFrameOfReferenceSequence).Items.Single()
            .GetSequence(DicomTag.RTReferencedStudySequence).Items.Single()
            .GetSequence(DicomTag.RTReferencedSeriesSequence).Items.Single()
            .GetSequence(DicomTag.ContourImageSequence).Items.Select(i => i.GetString(DicomTag.ReferencedSOPInstanceUID)).ToList();
    }

    /// <summary>The three ROI sequences hold exactly <paramref name="names"/>, numbered 1..n and cross-referenced.</summary>
    private static void AssertOnlyRois(DicomDataset rt, params string[] names)
    {
        int[] numbers = Enumerable.Range(1, names.Length).ToArray();
        List<DicomDataset> structureSet = rt.GetSequence(DicomTag.StructureSetROISequence).Items.ToList();
        Assert.Equal(names, structureSet.Select(i => i.GetString(DicomTag.ROIName)));
        Assert.Equal(numbers, structureSet.Select(i => i.GetSingleValue<int>(DicomTag.ROINumber)));
        Assert.Equal(numbers, rt.GetSequence(DicomTag.ROIContourSequence).Items.Select(i => i.GetSingleValue<int>(DicomTag.ReferencedROINumber)));
        List<DicomDataset> observations = rt.GetSequence(DicomTag.RTROIObservationsSequence).Items.ToList();
        Assert.Equal(numbers, observations.Select(i => i.GetSingleValue<int>(DicomTag.ReferencedROINumber)));
        Assert.Equal(numbers, observations.Select(i => i.GetSingleValue<int>(DicomTag.ObservationNumber)));
        Assert.All(observations, o => Assert.Single(o.GetSequence(DicomTag.RTROIIdentificationCodeSequence).Items));
    }

    [Fact]
    public void No_rt_is_written_when_no_roi_can_be_added()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct");
        string templates = folder.Sub("Templates");
        var rois = new List<ROIClass>
        {
            new ROIClass(0, 0, 255, "NoType", null, new OntologyCodeClass("Lung", "7195", "FMA")),
            new ROIClass(0, 255, 0, "   ", "ORGAN", new OntologyCodeClass("Liver", "7197", "FMA")),
        };
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images }, rois);

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.Written);
        Assert.Equal(2, report.RoiFailureCount);
        RunError error = Assert.Single(report.Errors);
        Assert.Equal(series.SeriesInstanceUid, error.SeriesInstanceUid);
        Assert.Equal("Brain_Test", error.TemplateName);
        Assert.Empty(Directory.GetFiles(images, "Brain_Test_UID*"));
    }
}
