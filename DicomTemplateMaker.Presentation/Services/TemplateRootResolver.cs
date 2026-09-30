using System;
using System.IO;
using System.Security;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>Where the template folder chosen at startup came from.</summary>
    public enum TemplateRootSource
    {
        /// <summary>The folder saved in the window settings.</summary>
        Saved,

        /// <summary>The working directory ("Start in" of a shortcut), which holds templates while the program folder does not.</summary>
        WorkingDirectory,

        /// <summary>The program folder.</summary>
        ProgramFolder,
    }

    /// <param name="Root">The template folder, as a full path.</param>
    /// <param name="Source">Which rule chose it.</param>
    /// <param name="ShouldSave">True when <paramref name="Root"/> should be saved as the template folder.</param>
    /// <param name="Note">Something to tell the user (the saved folder was not found), or null.</param>
    public sealed record TemplateRootResolution(string Root, TemplateRootSource Source, bool ShouldSave, string? Note)
    {
        public string Describe()
        {
            return Source switch
            {
                TemplateRootSource.Saved => "the saved template folder",
                TemplateRootSource.WorkingDirectory => "the folder the program was started in",
                _ => "the program folder",
            };
        }
    }

    /// <summary>Decides the template folder once at startup.</summary>
    public static class TemplateRootResolver
    {
        public const string OntologiesFolderName = "Ontologies";

        /// <summary>
        /// The saved folder when it exists. Otherwise the working directory when it differs from the program folder
        /// and holds a template layout (see <see cref="HasTemplateLayout"/>) while the program folder does not: that
        /// keeps setups started from a shortcut with a "Start in" folder working, and the folder is then saved unless
        /// another one was saved. Otherwise the program folder. A saved folder that is missing (a disconnected drive)
        /// is not replaced, and <see cref="TemplateRootResolution.Note"/> says so.
        /// </summary>
        public static TemplateRootResolution Resolve(string? savedRoot, string workingDirectory, string programDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(programDirectory);
            string program = Normalize(programDirectory);
            string working = Normalize(workingDirectory);
            bool hasSaved = !string.IsNullOrWhiteSpace(savedRoot);
            if (hasSaved)
            {
                string? saved = TryNormalize(savedRoot!);
                if (saved != null && Directory.Exists(saved))
                {
                    return new TemplateRootResolution(saved, TemplateRootSource.Saved, false, null);
                }
            }

            string root;
            TemplateRootSource source;
            if (!SameFolder(working, program) && HasTemplateLayout(working) && !HasTemplateLayout(program))
            {
                root = working;
                source = TemplateRootSource.WorkingDirectory;
            }
            else
            {
                root = program;
                source = TemplateRootSource.ProgramFolder;
            }

            string? note = hasSaved
                ? $"The saved template folder {savedRoot!.Trim()} was not found (is a drive disconnected?). This session uses {root} instead. "
                  + "The saved folder is kept for next time; use \"Change template folder\" to choose another one."
                : null;
            return new TemplateRootResolution(root, source, ShouldSave: !hasSaved && source == TemplateRootSource.WorkingDirectory, note);
        }

        /// <summary>
        /// True when <paramref name="folder"/> looks like a template folder: it has an Ontologies folder, or a
        /// sub-folder with All_ROIs.json, Paths.txt or a legacy ROIs folder. False when it cannot be read.
        /// </summary>
        public static bool HasTemplateLayout(string folder)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    return false;
                }

                if (Directory.Exists(Path.Combine(folder, OntologiesFolderName)))
                {
                    return true;
                }

                foreach (string sub in Directory.EnumerateDirectories(folder))
                {
                    if (File.Exists(Path.Combine(sub, ROIOntologyClass.ROIClassTools.RoisFileName))
                        || File.Exists(Path.Combine(sub, "Paths.txt"))
                        || Directory.Exists(Path.Combine(sub, "ROIs")))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                return false;
            }
        }

        /// <summary>True when both paths name the same folder (case-insensitive on Windows and macOS).</summary>
        public static bool SameFolder(string a, string b)
        {
            StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Normalize(a), Normalize(b), comparison);
        }

        /// <summary>The full path without a trailing separator (a drive root keeps its separator).</summary>
        public static string Normalize(string folder)
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        }

        private static string? TryNormalize(string folder)
        {
            try
            {
                return Normalize(folder.Trim());
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is SecurityException)
            {
                return null;
            }
        }
    }
}
