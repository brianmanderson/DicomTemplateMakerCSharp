using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DicomTemplateMakerGUI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Shell
{
    public enum TemplateProblemKind
    {
        /// <summary>The ontology library (Ontologies\All_Ontologies.json) cannot be read, so no template is listed.</summary>
        LibraryUnreadable,

        /// <summary>A template's files cannot be read; it is not listed and nothing is saved over them.</summary>
        TemplateUnreadable,

        /// <summary>
        /// Legacy text files that could not be migrated were kept next to the migrated template or library; reported on
        /// every scan while they are there.
        /// </summary>
        LegacyFilesKept,

        /// <summary>The template folder itself cannot be listed.</summary>
        FolderUnreadable,

        /// <summary>The templates' codes could not be added to the ontology library.</summary>
        LibraryNotUpdated,
    }

    /// <summary>Something wrong with the template folder, for the problems panel.</summary>
    /// <param name="Subject">The template (folder name), "Ontologies", or the template folder.</param>
    public sealed record TemplateProblem(TemplateProblemKind Kind, string Subject, string Message)
    {
        /// <summary>Identifies the problem, so it is shown in a message box once per session.</summary>
        public string Key => Kind + "\u001f" + Subject + "\u001f" + Message;

        public string Describe()
        {
            return Kind switch
            {
                TemplateProblemKind.LibraryUnreadable => "Ontology library: " + Message,
                TemplateProblemKind.FolderUnreadable => Message,
                _ => Subject + ": " + Message,
            };
        }
    }

    public sealed class TemplateScanResult
    {
        public TemplateScanResult(IReadOnlyList<TemplateMaker> templates, IReadOnlyList<TemplateProblem> problems)
        {
            Templates = templates;
            Problems = problems;
        }

        /// <summary>The readable templates, sorted by name; each has its own copy of the ontology library.</summary>
        public IReadOnlyList<TemplateMaker> Templates { get; }

        public IReadOnlyList<TemplateProblem> Problems { get; }
    }

    /// <summary>
    /// Reads every template in a template folder for the main window's list. Safe to run off the UI thread: it only
    /// creates <see cref="TemplateMaker"/> objects.
    /// </summary>
    public static class TemplateLibraryScanner
    {
        /// <summary>
        /// Reads the ontology library once, gives each template its own copy of it (the editors change entries in
        /// place), reads each sub-folder with <see cref="TemplateMaker.categorize_folder"/>, and then adds all the
        /// templates' codes to the library in one step (instead of one library read, and possibly write, per template).
        /// Templates that cannot be read are left out and reported; so is every template when the library itself
        /// cannot be read, since saving any of them would need it.
        /// </summary>
        public static TemplateScanResult Scan(string templateRoot, ILogger? logger = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateRoot);
            logger ??= NullLogger.Instance;
            string ontoPath = Path.Combine(templateRoot, TemplateRootResolver.OntologiesFolderName);
            var problems = new List<TemplateProblem>();

            string libraryJson;
            var libraryWarnings = new List<string>();
            try
            {
                List<OntologyCodeClass> library = OntologyTools.LoadOntologiesFromFolder(ontoPath, libraryWarnings);
                libraryJson = JsonConvert.SerializeObject(library);
            }
            catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
            {
                problems.Add(new TemplateProblem(TemplateProblemKind.LibraryUnreadable, TemplateRootResolver.OntologiesFolderName,
                    ex.Message + " No template is listed until it is repaired or removed, because saving a template needs it."));
                return new TemplateScanResult(Array.Empty<TemplateMaker>(), problems);
            }

            problems.AddRange(libraryWarnings.Select(w => new TemplateProblem(TemplateProblemKind.LegacyFilesKept, TemplateRootResolver.OntologiesFolderName, w)));

            string[] folders;
            try
            {
                folders = Directory.GetDirectories(templateRoot);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                problems.Add(new TemplateProblem(TemplateProblemKind.FolderUnreadable, templateRoot, $"The template folder {templateRoot} cannot be read: {ex.Message}"));
                return new TemplateScanResult(Array.Empty<TemplateMaker>(), problems);
            }

            Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
            var templates = new List<TemplateMaker>();
            foreach (string folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileName(folder);
                if (string.Equals(name, TemplateRootResolver.OntologiesFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TemplateMaker maker = new TemplateMaker();
                maker.define_path(folder);
                maker.define_output(folder);
                maker.Ontologies = CopyLibrary(libraryJson);
                try
                {
                    // onto_path is set afterwards, so categorize_folder does not read the library again for this template.
                    maker.categorize_folder();
                }
                catch (TemplateLoadException ex)
                {
                    problems.Add(new TemplateProblem(TemplateProblemKind.TemplateUnreadable, name, ex.Message + " The template is not listed, and nothing is saved over the file."));
                    continue;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    problems.Add(new TemplateProblem(TemplateProblemKind.TemplateUnreadable, name, $"Could not read {folder}: {ex.Message} The template is not listed."));
                    continue;
                }

                maker.set_onto_path(ontoPath);
                if (!maker.is_template)
                {
                    continue;
                }

                templates.Add(maker);
                problems.AddRange(maker.LoadWarnings.Select(w => new TemplateProblem(TemplateProblemKind.LegacyFilesKept, name, w)));
            }

            if (templates.Count > 0)
            {
                MergeTemplateCodesIntoLibrary(templates, ontoPath, problems, logger);
            }

            logger.LogDebug("Read {Count} template(s) from {TemplateRoot} with {Problems} problem(s).", templates.Count, templateRoot, problems.Count);
            return new TemplateScanResult(templates, problems);
        }

        /// <summary>A private copy of the library, sorted as the editors expect.</summary>
        private static List<OntologyCodeClass> CopyLibrary(string libraryJson)
        {
            List<OntologyCodeClass> copy = JsonConvert.DeserializeObject<List<OntologyCodeClass>>(libraryJson) ?? new List<OntologyCodeClass>();
            copy.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
            return copy;
        }

        private static void MergeTemplateCodesIntoLibrary(List<TemplateMaker> templates, string ontoPath, List<TemplateProblem> problems, ILogger logger)
        {
            try
            {
                int added = OntologyTools.MergeOntologiesIntoFolder(templates.SelectMany(t => t.ROIs.Select(roi => roi.Ontology_Class)), ontoPath);
                if (added == 0)
                {
                    return;
                }

                logger.LogInformation("Added {Count} code(s) found in templates to the ontology library in {Path}.", added, ontoPath);
                // Each template's list now also offers the codes other templates brought in, as a per-template library
                // read used to.
                string merged = JsonConvert.SerializeObject(OntologyTools.LoadOntologiesFromFolder(ontoPath));
                foreach (TemplateMaker template in templates)
                {
                    bool grew = false;
                    foreach (OntologyCodeClass entry in CopyLibrary(merged))
                    {
                        if (!template.Ontologies.Any(o => o.CodeValue == entry.CodeValue && o.Scheme == entry.Scheme))
                        {
                            template.Ontologies.Add(entry);
                            grew = true;
                        }
                    }

                    if (grew)
                    {
                        template.Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
                    }
                }
            }
            catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
            {
                problems.Add(new TemplateProblem(TemplateProblemKind.LibraryNotUpdated, TemplateRootResolver.OntologiesFolderName,
                    "The codes used by the templates could not be added to the ontology library: " + ex.Message));
            }
        }
    }
}
