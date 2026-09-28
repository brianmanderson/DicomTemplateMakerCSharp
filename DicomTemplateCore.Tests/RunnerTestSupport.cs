using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Services;
using FellowOakDicom;
using Microsoft.Extensions.Logging;
using ROIOntologyClass;

namespace DicomTemplateCore.Tests;

/// <summary>A clock set by hand. Its local time zone is a fixed UTC+2 zone, so local and UTC dates differ visibly.</summary>
internal sealed class RunnerClock : TimeProvider
{
    public static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone("Test+02", TimeSpan.FromHours(2), "Test+02", "Test+02");

    private DateTimeOffset now;

    public RunnerClock(DateTimeOffset now)
    {
        this.now = now;
    }

    public override TimeZoneInfo LocalTimeZone => PlusTwo;

    /// <summary>A clock an hour ahead of the real one, so every file the test just wrote counts as settled.</summary>
    public static RunnerClock AfterFilesSettled() => new RunnerClock(DateTimeOffset.UtcNow.AddHours(1));

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}

/// <summary>A synthetic series written by <see cref="SeriesFactory"/>.</summary>
internal sealed record WrittenSeries(string SeriesInstanceUid, string StudyInstanceUid, string FrameOfReferenceUid, IReadOnlyList<string> Files);

/// <summary>Writes copies of the bundled four-slice CT (Visible Human, public domain) as new series.</summary>
internal static class SeriesFactory
{
    public static string NewUid() => DicomUIDGenerator.GenerateDerivedFromUUID().UID;

    /// <summary>
    /// Writes the sample CT into <paramref name="directory"/> as <c>{prefix}1.dcm</c> ... <c>{prefix}4.dcm</c> with new
    /// series, study, frame of reference and SOP instance UIDs (unless given), then applies <paramref name="edit"/>
    /// to every slice.
    /// </summary>
    public static WrittenSeries Write(string directory, string prefix, Action<DicomDataset>? edit = null, string? seriesUid = null, string? studyUid = null)
    {
        seriesUid ??= NewUid();
        studyUid ??= NewUid();
        string frameOfReference = NewUid();
        var files = new List<string>();
        int index = 1;
        foreach (string source in Directory.GetFiles(TestFolder.Data("SmallCT")).OrderBy(f => f, StringComparer.Ordinal))
        {
            DicomDataset dataset = DicomFile.Open(source, FileReadOption.ReadAll).Dataset.Clone();
            dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, seriesUid);
            dataset.AddOrUpdate(DicomTag.StudyInstanceUID, studyUid);
            dataset.AddOrUpdate(DicomTag.FrameOfReferenceUID, frameOfReference);
            dataset.AddOrUpdate(DicomTag.SOPInstanceUID, NewUid());
            edit?.Invoke(dataset);
            string path = Path.Combine(directory, $"{prefix}{index++}.dcm");
            new DicomFile(dataset).Save(path);
            files.Add(path);
        }

        return new WrittenSeries(seriesUid, studyUid, frameOfReference, files);
    }

    public static Action<DicomDataset> Descriptions(string? series, string? study) => dataset =>
    {
        Set(dataset, DicomTag.SeriesDescription, series);
        Set(dataset, DicomTag.StudyDescription, study);
    };

    private static void Set(DicomDataset dataset, DicomTag tag, string? value)
    {
        if (value == null)
        {
            dataset.Remove(tag);
        }
        else
        {
            dataset.AddOrUpdate(tag, value);
        }
    }
}

/// <summary>Template folders for runner tests.</summary>
internal static class RunnerTemplates
{
    public static List<ROIClass> DefaultRois() => new List<ROIClass>
    {
        new ROIClass(255, 0, 0, "Brain", "ORGAN", new OntologyCodeClass("Brain", "50801", "FMA")),
        new ROIClass(0, 255, 0, "PTV_High", "PTV", new OntologyCodeClass("PTV_High", "PTV_High", "99VMS_STRUCTCODE")),
    };

    /// <summary>Writes template <paramref name="name"/> under <paramref name="templateRoot"/> with the given paths, ROIs and DicomTags.txt entries.</summary>
    public static string Make(string templateRoot, string name, IEnumerable<string> paths, IEnumerable<ROIClass>? rois = null, Dictionary<string, List<string>>? dicomTags = null)
    {
        var maker = new TemplateMaker();
        string folder = Path.Combine(templateRoot, name);
        maker.define_output(folder);
        maker.set_onto_path(Path.Combine(templateRoot, "Ontologies"));
        maker.Paths.AddRange(paths);
        maker.ROIs.AddRange(rois ?? DefaultRois());
        if (dicomTags != null)
        {
            maker.DicomTags = dicomTags;
        }

        maker.make_template();
        return folder;
    }

    public static Dictionary<string, List<string>> SeriesRequirement(params string[] values) =>
        new Dictionary<string, List<string>> { { "Series Description", values.ToList() } };

    public static DicomTemplateRunner Runner(string templateRoot, TimeProvider clock) =>
        new DicomTemplateRunner(templateRoot, DicomTemplateRunner.DefaultTemplateRsPath, null, clock);

    public static string OutputPath(string directory, string templateName, string seriesUid) =>
        Path.Combine(directory, $"{templateName}_UID{seriesUid}.dcm");

    public static DicomFile ReadOutput(string directory, string templateName, string seriesUid) =>
        DicomFile.Open(OutputPath(directory, templateName, seriesUid));
}

/// <summary>Keeps every log entry (level and formatted message).</summary>
internal sealed class CapturingLogger : ILogger
{
    private readonly object gate = new object();
    private readonly List<(LogLevel Level, string Message)> entries = new List<(LogLevel, string)>();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (gate)
            {
                return entries.ToList();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (gate)
        {
            entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
