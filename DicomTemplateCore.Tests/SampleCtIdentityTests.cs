using FellowOakDicom;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>
/// The bundled sample CT carries a fictitious test patient (overhaul plan P0.2, <c>tools/reidentify_sample_ct.py</c>).
/// Every RT the program writes copies its patient attributes, so a real identity here would be in every test RT and
/// every file the CI validation job produces.
/// </summary>
public class SampleCtIdentityTests
{
    private static List<DicomFile> Slices() =>
        Directory.GetFiles(TestFolder.Data("SmallCT")).Order(StringComparer.Ordinal).Select(f => DicomFile.Open(f)).ToList();

    private static bool IsAbsentOrEmpty(DicomDataset dataset, DicomTag tag) =>
        !dataset.Contains(tag) || dataset.GetValueCount(tag) == 0;

    [Fact]
    public void The_sample_ct_has_four_slices()
    {
        Assert.Equal(4, Slices().Count);
    }

    [Fact]
    public void Every_slice_carries_the_fictitious_test_patient()
    {
        foreach (DicomFile file in Slices())
        {
            DicomDataset slice = file.Dataset;
            Assert.Equal("ZZTEST^TG263TEMPLATE", slice.GetString(DicomTag.PatientName));
            Assert.Equal("ZZTEST-0001", slice.GetString(DicomTag.PatientID));
            Assert.Equal("YES", slice.GetString(DicomTag.PatientIdentityRemoved));
            Assert.True(IsAbsentOrEmpty(slice, DicomTag.PatientBirthDate));
            Assert.True(IsAbsentOrEmpty(slice, DicomTag.PatientBirthTime));
        }
    }

    [Fact]
    public void All_slices_share_one_study_series_and_frame_of_reference_with_distinct_instances()
    {
        List<DicomFile> slices = Slices();

        Assert.Single(slices.Select(s => s.Dataset.GetString(DicomTag.StudyInstanceUID)).Distinct());
        Assert.Single(slices.Select(s => s.Dataset.GetString(DicomTag.SeriesInstanceUID)).Distinct());
        Assert.Single(slices.Select(s => s.Dataset.GetString(DicomTag.FrameOfReferenceUID)).Distinct());
        Assert.Equal(slices.Count, slices.Select(s => s.Dataset.GetString(DicomTag.SOPInstanceUID)).Distinct().Count());
        foreach (DicomFile file in slices)
        {
            Assert.Equal(file.Dataset.GetString(DicomTag.SOPInstanceUID), file.FileMetaInfo.MediaStorageSOPInstanceUID.UID);
        }
    }
}
