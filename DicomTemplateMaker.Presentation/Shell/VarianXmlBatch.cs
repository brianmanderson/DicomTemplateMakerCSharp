using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Shell
{
    /// <summary>A template to export: its name (the file name), folder and the ontology library to resolve its codes.</summary>
    public sealed record VarianExportItem(string TemplateName, string TemplateFolder, List<OntologyCodeClass> Ontologies);

    public sealed class VarianExportResult
    {
        public VarianExportResult(string outputFolder)
        {
            OutputFolder = outputFolder;
        }

        public string OutputFolder { get; }

        /// <summary>The reports of the files written.</summary>
        public List<VarianXmlReport> Written { get; } = new List<VarianXmlReport>();

        /// <summary>File names left alone because they existed and the user chose not to replace them.</summary>
        public List<string> SkippedExisting { get; } = new List<string>();

        /// <summary>Templates not exported, and why.</summary>
        public List<(string Template, string Reason)> Failed { get; } = new List<(string, string)>();
    }

    /// <summary>One XML file found for import, with the template it would create.</summary>
    /// <param name="TemplateName">The template's folder name (the Preview ID, spaces as underscores); null when unknown.</param>
    /// <param name="Exists">True when that template exists and importing would replace its ROIs.</param>
    /// <param name="Problem">Why the file is not imported (checked before importing); null when it is.</param>
    public sealed record VarianImportCandidate(string File, string? TemplateName, bool Exists, string? Problem);

    public sealed class VarianImportResult
    {
        public VarianImportResult(string templateRoot)
        {
            TemplateRoot = templateRoot;
        }

        public string TemplateRoot { get; }

        /// <summary>One report per file that was imported or failed while importing.</summary>
        public List<VarianXmlReport> Reports { get; } = new List<VarianXmlReport>();

        /// <summary>
        /// Existing templates left unchanged: because the user chose not to replace them, or because they appeared after
        /// the user was asked.
        /// </summary>
        public List<string> SkippedExisting { get; } = new List<string>();

        /// <summary>Files not imported because of a problem found beforehand.</summary>
        public List<(string File, string Reason)> NotImported { get; } = new List<(string, string)>();

        public IEnumerable<VarianXmlReport> Imported => Reports.Where(r => !r.Failed && r.Target != null);

        public IEnumerable<VarianXmlReport> FailedReports => Reports.Where(r => r.Failed);
    }

    /// <summary>Varian XML import and export of several templates; everything here can run off the UI thread.</summary>
    public static class VarianXmlBatch
    {
        /// <summary>The export file name of a template.</summary>
        public static string ExportFileName(string templateName)
        {
            return templateName + ".xml";
        }

        /// <summary>The export files of <paramref name="templateNames"/> that already exist in <paramref name="outputFolder"/>.</summary>
        public static IReadOnlyList<string> ExistingExportFiles(IEnumerable<string> templateNames, string outputFolder)
        {
            return templateNames.Select(ExportFileName).Where(file => File.Exists(Path.Combine(outputFolder, file))).ToList();
        }

        /// <summary>
        /// Writes one XML file per template. An existing file is replaced only when <paramref name="replaceExisting"/>
        /// is true, and then in one step (a failed write leaves the old file). A template that cannot be read, or whose
        /// file cannot be written, is reported and the others go on.
        /// </summary>
        public static VarianExportResult Export(IReadOnlyList<VarianExportItem> items, string outputFolder, bool replaceExisting, IProgress<string>? progress, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(items);
            var result = new VarianExportResult(outputFolder);
            for (int i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VarianExportItem item = items[i];
                progress?.Report($"Writing Varian XML: {i + 1} of {items.Count} ({item.TemplateName})");
                string fileName = ExportFileName(item.TemplateName);
                string target = Path.Combine(outputFolder, fileName);
                if (!replaceExisting && File.Exists(target))
                {
                    result.SkippedExisting.Add(fileName);
                    continue;
                }

                var writer = new VarianXmlWriter();
                VarianXmlReport report;
                try
                {
                    report = writer.LoadROIsFromPath(item.TemplateFolder, item.Ontologies);
                }
                catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    result.Failed.Add((item.TemplateName, ex.Message));
                    continue;
                }

                if (report.Failed)
                {
                    result.Failed.Add((item.TemplateName, report.Error ?? "the template could not be exported."));
                    continue;
                }

                try
                {
                    AtomicTextFile.WriteVia(target, writer.SaveFile);
                    result.Written.Add(report);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is XmlException)
                {
                    result.Failed.Add((item.TemplateName, $"{target} could not be written: {ex.Message}"));
                }
            }

            return result;
        }

        /// <summary>
        /// Lists the *.xml files of <paramref name="xmlFolder"/> with the template each would create in
        /// <paramref name="templateRoot"/>. A file that cannot be read, holds no template name, or whose name is not a
        /// usable template name (see <see cref="NameRules.TemplateNameProblem"/>: no path characters, no "Ontologies", no
        /// Windows device names or trailing dots) or repeats the name of an earlier file gets a
        /// <see cref="VarianImportCandidate.Problem"/> and is not imported.
        /// </summary>
        public static IReadOnlyList<VarianImportCandidate> PlanImport(string xmlFolder, string templateRoot)
        {
            var candidates = new List<VarianImportCandidate>();
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.GetFiles(xmlFolder, "*.xml").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                (string? name, string? readProblem) = ReadTemplateName(file);
                string? nameProblem = name == null ? null : ImportNameProblem(name);
                if (readProblem != null)
                {
                    candidates.Add(new VarianImportCandidate(file, null, false, readProblem));
                }
                else if (name == null)
                {
                    candidates.Add(new VarianImportCandidate(file, null, false, "it holds no template name (the Preview element has no ID)."));
                }
                else if (nameProblem != null)
                {
                    candidates.Add(new VarianImportCandidate(file, name, false, $"its template name \"{name}\" cannot be used: {nameProblem}"));
                }
                else if (seen.TryGetValue(name, out string? first))
                {
                    candidates.Add(new VarianImportCandidate(file, name, false, $"{Path.GetFileName(first)} in the same folder also holds template \"{name}\"; only that file is imported."));
                }
                else
                {
                    seen.Add(name, file);
                    candidates.Add(new VarianImportCandidate(file, name, TemplateMaker.TemplateExists(Path.Combine(templateRoot, name)), null));
                }
            }

            return candidates;
        }

        /// <summary>
        /// Imports the candidates without a problem with <see cref="VarianXmlReader.Import"/>. Candidates whose template
        /// existed when planning (<see cref="VarianImportCandidate.Exists"/>, the templates the user was asked about) are
        /// imported only when <paramref name="replaceExisting"/> is true. Each file's template name is read again first
        /// (the user may have taken a while to answer): a file whose name changed is not imported, and a template that
        /// has appeared since the plan is always kept, because the user was not asked about it.
        /// </summary>
        public static VarianImportResult Import(IReadOnlyList<VarianImportCandidate> candidates, string templateRoot, bool replaceExisting, IProgress<string>? progress, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            var result = new VarianImportResult(templateRoot);
            for (int i = 0; i < candidates.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VarianImportCandidate candidate = candidates[i];
                if (candidate.Problem != null || candidate.TemplateName == null)
                {
                    result.NotImported.Add((candidate.File, candidate.Problem ?? "it holds no template name."));
                    continue;
                }

                (string? name, string? readProblem) = ReadTemplateName(candidate.File);
                if (readProblem != null || !string.Equals(name, candidate.TemplateName, StringComparison.Ordinal))
                {
                    result.NotImported.Add((candidate.File, readProblem ?? $"the file changed after it was read (its template name is now \"{name}\"); import it again."));
                    continue;
                }

                bool keep = candidate.Exists
                    ? !replaceExisting
                    : TemplateMaker.TemplateExists(Path.Combine(templateRoot, candidate.TemplateName));
                if (keep)
                {
                    result.SkippedExisting.Add(candidate.TemplateName);
                    continue;
                }

                progress?.Report($"Importing Varian XML: {i + 1} of {candidates.Count} ({Path.GetFileName(candidate.File)})");
                result.Reports.Add(VarianXmlReader.Import(candidate.File, templateRoot));
            }

            return result;
        }

        /// <summary>The template name <see cref="VarianXmlReader.XmlToROI"/> would use, or null when the file has none or cannot be read.</summary>
        internal static string? PeekTemplateName(string xmlFile)
        {
            return ReadTemplateName(xmlFile).Name;
        }

        /// <summary>
        /// The template name <see cref="VarianXmlReader.XmlToROI"/> would use (null when the file has none), or why the
        /// file could not be read.
        /// </summary>
        private static (string? Name, string? Problem) ReadTemplateName(string xmlFile)
        {
            try
            {
                string? id = XDocument.Load(xmlFile).Root?.Element("Preview")?.Attribute("ID")?.Value;
                return (string.IsNullOrWhiteSpace(id) ? null : id.Replace(' ', '_'), null);
            }
            catch (Exception ex) when (ex is XmlException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return (null, "it could not be read: " + ex.Message);
            }
        }

        /// <summary>Why <paramref name="name"/> cannot be a template folder (the editors' rule), or null when it can.</summary>
        private static string? ImportNameProblem(string name)
        {
            return NameRules.TemplateNameProblem(name);
        }
    }
}
