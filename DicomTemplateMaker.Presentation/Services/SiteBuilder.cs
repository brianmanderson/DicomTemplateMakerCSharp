using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ROIOntologyClass;
using TemplateSync.Model;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>Why <see cref="SiteBuilder"/> reported a site or one of its records.</summary>
    public enum SiteBuildWarningKind
    {
        /// <summary>A record has no Structure name; it is skipped.</summary>
        EmptyName,

        /// <summary>A name has no usable characters and there is no code; the record is skipped.</summary>
        NoUsableCharacters,

        /// <summary>A record has no valid colour; its ROI is kept in red.</summary>
        RedFallbackColour,

        /// <summary>A DVH colour does not parse; the ROI colour is used.</summary>
        BadDvhColour,

        /// <summary>A code lacks a value or a scheme; generated RTs leave the ROI out.</summary>
        IncompleteCode,

        /// <summary>A value of a record could not be read; the record is skipped.</summary>
        UnreadableRecord,

        /// <summary>The site name is not a single folder name; the site is not built.</summary>
        UnusableSiteName,
    }

    /// <summary>One warning of a build. <see cref="RecordId"/> and <see cref="Structure"/> are null for a site-level warning.</summary>
    public sealed record SiteBuildWarning(string Site, string? RecordId, string? Structure, SiteBuildWarningKind Kind, string Message);

    /// <summary>The language and laterality choice of the Online Templates window.</summary>
    /// <param name="Language">One of the window's language choices; matched on "French", "Spanish" or "English".</param>
    /// <param name="LateralityFirst">Name lateral structures with the side first (L_Breast rather than Breast_L).</param>
    /// <param name="LanguageVisible">Whether the window offers the language choice (some ROI has another language).</param>
    /// <param name="LateralityVisible">Whether the window offers the laterality choice (some ROI is lateral).</param>
    public sealed record NamingChoice(string Language, bool LateralityFirst, bool LanguageVisible, bool LateralityVisible)
    {
        /// <summary>The window's first (default) language choice.</summary>
        public const string English = "English/Ingles/Anglais";

        /// <summary>
        /// What the window does when nothing is changed: English, side last, with the choices visible exactly when
        /// some site of <paramref name="index"/> offers them (the window looks at every site of the source).
        /// </summary>
        public static NamingChoice Default(TemplateIndex index)
        {
            ArgumentNullException.ThrowIfNull(index);
            bool anyLateral = false;
            bool anyOtherLanguage = false;
            var ignored = new List<SiteBuildWarning>();
            foreach (string site in index.SiteNames)
            {
                List<ROIWrapper> wrappers = SiteBuilder.BuildRoiWrappers(index, site, ignored);
                anyLateral |= wrappers.Any(x => x.has_lateral);
                anyOtherLanguage |= wrappers.Any(x => x.has_other_lanuages);
            }

            return new NamingChoice(English, LateralityFirst: false, anyOtherLanguage, anyLateral);
        }
    }

    /// <summary>One site to build: its folder name and its ROIs, already renamed by the naming choice.</summary>
    public sealed record SiteJob(string Site, List<ROIClass> Rois);

    /// <summary>A build that stopped: how many templates were written before <see cref="Exception.InnerException"/>.</summary>
    public sealed class SiteBuildException : Exception
    {
        public SiteBuildException(int built, Exception inner)
            : base(inner.Message, inner)
        {
            Built = built;
        }

        public int Built { get; }
    }

    /// <summary>
    /// Builds template folders from the sites of a template table, as the Online Templates window does: one folder
    /// per site directly inside the template folder, the ontology library read first and updated once at the end.
    /// Every warning is returned as a <see cref="SiteBuildWarning"/>, without a cap.
    /// </summary>
    public sealed class SiteBuilder
    {
        /// <param name="templateFolder">Where template folders are built (one folder per site).</param>
        /// <param name="ontologyFolder">The ontology library the builder updates.</param>
        public SiteBuilder(string templateFolder, string ontologyFolder)
        {
            TemplateFolder = templateFolder ?? throw new ArgumentNullException(nameof(templateFolder));
            OntologyFolder = ontologyFolder ?? throw new ArgumentNullException(nameof(ontologyFolder));
        }

        public string TemplateFolder { get; }

        public string OntologyFolder { get; }

        public string TemplatePath(string site) => Path.Combine(TemplateFolder, site);

        /// <summary>
        /// A site becomes one folder directly inside the template folder, so its name must be a single valid
        /// folder name: no separators or other invalid characters, and not "." or "..".
        /// </summary>
        public static bool IsUsableFolderName(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && name != "."
                && name != ".."
                && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && name.IndexOf(Path.DirectorySeparatorChar) < 0
                && name.IndexOf(Path.AltDirectorySeparatorChar) < 0;
        }

        /// <summary>Fresh ROI objects for one site, in index order; records that cannot be used are reported and skipped.</summary>
        public static List<ROIWrapper> BuildRoiWrappers(TemplateIndex index, string site, ICollection<SiteBuildWarning> warnings)
        {
            ArgumentNullException.ThrowIfNull(index);
            ArgumentNullException.ThrowIfNull(warnings);
            var wrappers = new List<ROIWrapper>();
            foreach (SiteRoi siteRoi in index.GetSite(site))
            {
                AirTableEntry entry = siteRoi.Entry;
                ROIWrapper? wrapper = TemplateEntryMapper.ToRoiWrapper(siteRoi, (problem, message) =>
                    warnings.Add(new SiteBuildWarning(site, entry.Id, entry.Structure, ToKind(problem), message)));
                if (wrapper != null)
                {
                    wrappers.Add(wrapper);
                }
            }

            return wrappers;
        }

        /// <summary>Applies the language and laterality choices exactly as the window always has.</summary>
        public static void ApplyNamingChoice(List<ROIWrapper> wrappers, NamingChoice naming)
        {
            ArgumentNullException.ThrowIfNull(wrappers);
            ArgumentNullException.ThrowIfNull(naming);
            string language = naming.Language ?? string.Empty;
            bool reverse = naming.LateralityFirst;
            foreach (ROIWrapper r_wrapper in wrappers)
            {
                if (naming.LanguageVisible)
                {
                    if (language.Contains("French", StringComparison.Ordinal))
                    {
                        if (naming.LateralityVisible) { r_wrapper.Set_French(reverse); } else { r_wrapper.Set_French(); }
                    }
                    else if (language.Contains("Spanish", StringComparison.Ordinal))
                    {
                        if (naming.LateralityVisible) { r_wrapper.Set_Spanish(reverse); } else { r_wrapper.Set_Spanish(); }
                    }
                    else if (language.Contains("English", StringComparison.Ordinal))
                    {
                        if (naming.LateralityVisible) { r_wrapper.Set_English(reverse); } else { r_wrapper.Set_English(); }
                    }
                }
                else if (naming.LateralityVisible)
                {
                    r_wrapper.Set_English(reverse);
                }
            }
        }

        /// <summary>The ROIs of one site with the naming choice applied. The site name is not checked here.</summary>
        public static SiteJob Prepare(TemplateIndex index, string site, NamingChoice naming, ICollection<SiteBuildWarning> warnings)
        {
            List<ROIWrapper> wrappers = BuildRoiWrappers(index, site, warnings);
            ApplyNamingChoice(wrappers, naming);
            return new SiteJob(site, wrappers.Select(x => x.roi).ToList());
        }

        /// <summary>
        /// <see cref="Prepare"/> for each site in order; a site whose name is not a usable folder name is reported as
        /// <see cref="SiteBuildWarningKind.UnusableSiteName"/> and left out.
        /// </summary>
        public static List<SiteJob> PrepareAll(TemplateIndex index, IEnumerable<string> sites, NamingChoice naming, ICollection<SiteBuildWarning> warnings)
        {
            ArgumentNullException.ThrowIfNull(sites);
            ArgumentNullException.ThrowIfNull(warnings);
            var jobs = new List<SiteJob>();
            foreach (string site in sites)
            {
                if (!IsUsableFolderName(site))
                {
                    warnings.Add(new SiteBuildWarning(site, null, null, SiteBuildWarningKind.UnusableSiteName, "'" + site + "' is not a single folder name, so it was not built."));
                    continue;
                }

                jobs.Add(Prepare(index, site, naming, warnings));
            }

            return jobs;
        }

        /// <summary>
        /// Writes each job's template folder, one after another, then merges their codes into the ontology library.
        /// Throws <see cref="SiteBuildException"/> with the count built so far; templates finished before a failure
        /// stay built and still bring their codes into the library.
        /// </summary>
        public int Build(IReadOnlyList<SiteJob> jobs, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(jobs);
            int built = 0;
            try
            {
                // Read first: a library that cannot be read stops the build before any template is written.
                OntologyTools.LoadOntologiesFromFolder(OntologyFolder);
                foreach (SiteJob job in jobs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report("Building " + (built + 1) + " of " + jobs.Count + ": " + job.Site);
                    TemplateMaker evaluator = new TemplateMaker();
                    evaluator.define_output(TemplatePath(job.Site));
                    evaluator.ROIs = job.Rois;
                    evaluator.make_template();
                    built++;
                }
            }
            catch (Exception ex)
            {
                // The templates written so far still bring their codes into the library.
                if (built > 0)
                {
                    TryMergeCodes(jobs.Take(built));
                }

                throw new SiteBuildException(built, ex);
            }

            try
            {
                progress?.Report("Updating the ontology library…");
                OntologyTools.MergeOntologiesIntoFolder(jobs.SelectMany(j => j.Rois.Select(roi => roi.Ontology_Class)), OntologyFolder);
                return built;
            }
            catch (Exception ex)
            {
                throw new SiteBuildException(built, ex);
            }
        }

        private void TryMergeCodes(IEnumerable<SiteJob> jobs)
        {
            try
            {
                OntologyTools.MergeOntologiesIntoFolder(jobs.SelectMany(j => j.Rois.Select(roi => roi.Ontology_Class)), OntologyFolder);
            }
            catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // The build failure is what gets reported; the codes are added the next time the templates are read.
            }
        }

        private static SiteBuildWarningKind ToKind(TemplateEntryProblem problem) => problem switch
        {
            TemplateEntryProblem.EmptyName => SiteBuildWarningKind.EmptyName,
            TemplateEntryProblem.NoUsableCharacters => SiteBuildWarningKind.NoUsableCharacters,
            TemplateEntryProblem.RedFallbackColour => SiteBuildWarningKind.RedFallbackColour,
            TemplateEntryProblem.BadDvhColour => SiteBuildWarningKind.BadDvhColour,
            TemplateEntryProblem.IncompleteCode => SiteBuildWarningKind.IncompleteCode,
            TemplateEntryProblem.UnreadableRecord => SiteBuildWarningKind.UnreadableRecord,
            _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null),
        };
    }
}
