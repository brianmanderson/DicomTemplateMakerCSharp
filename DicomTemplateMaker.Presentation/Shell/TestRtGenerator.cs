using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DicomTemplateMakerGUI.DicomTemplateServices;

namespace DicomTemplateMakerGUI.Shell
{
    /// <summary>The outcome of "Create folder with loadable RTs".</summary>
    public sealed class TestRtResult
    {
        public TestRtResult(string outputFolder)
        {
            OutputFolder = outputFolder;
        }

        public string OutputFolder { get; }

        /// <summary>Sample CT files copied into the folder (files already there are kept).</summary>
        public int SampleFilesCopied { get; internal set; }

        /// <summary>One report per template, in the order asked.</summary>
        public List<(string Template, RunReport Report)> Reports { get; } = new List<(string, RunReport)>();

        /// <summary>Failures before any RT could be written (the sample CT could not be copied).</summary>
        public List<string> Errors { get; } = new List<string>();

        public int WrittenCount => Reports.Sum(r => r.Report.WrittenCount);

        public bool HasFailures => Errors.Count > 0 || Reports.Any(r => r.Report.ErrorCount > 0 || r.Report.RoiFailureCount > 0);
    }

    /// <summary>
    /// Writes test RTs: copies the bundled sample CT into a folder and runs each template once on it with
    /// <see cref="DicomTemplateRunner.RunForFolder"/>. Never touches a template's Paths.txt and never starts the
    /// background generator.
    /// </summary>
    public static class TestRtGenerator
    {
        public static TestRtResult Run(DicomTemplateRunner runner, IReadOnlyList<string> templateNames, string sampleFolder, string outputFolder, IProgress<string>? progress, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(runner);
            ArgumentNullException.ThrowIfNull(templateNames);
            var result = new TestRtResult(outputFolder);
            try
            {
                string[] samples = Directory.GetFiles(sampleFolder, "*.dcm");
                if (samples.Length == 0)
                {
                    result.Errors.Add($"The sample CT folder {sampleFolder} holds no images; reinstall the program.");
                    return result;
                }

                Directory.CreateDirectory(outputFolder);
                foreach (string sample in samples.OrderBy(f => f, StringComparer.Ordinal))
                {
                    string target = Path.Combine(outputFolder, Path.GetFileName(sample));
                    if (!File.Exists(target))
                    {
                        File.Copy(sample, target);
                        result.SampleFilesCopied++;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                result.Errors.Add($"The sample CT could not be copied from {sampleFolder} to {outputFolder}: {ex.Message}");
                return result;
            }

            for (int i = 0; i < templateNames.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = templateNames[i];
                progress?.Report($"Writing test RTs: {i + 1} of {templateNames.Count} ({name})");
                result.Reports.Add((name, runner.RunForFolder(name, outputFolder, cancellationToken)));
            }

            return result;
        }
    }
}
