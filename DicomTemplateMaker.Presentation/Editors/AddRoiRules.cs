using System;
using System.Collections.Generic;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Editors
{
    /// <summary>
    /// Whether the template editor's Add ROI button is enabled, and what the status line under it says: a problem (the
    /// name cannot be used), a warning (it can, but the user should know something) or a hint (a step not done yet).
    /// </summary>
    public sealed record AddRoiState(bool CanAdd, string? Problem, string? Warning, string? Hint);

    /// <summary>The rule for Add ROI: a usable name, a chosen ontology code and a chosen type, in that order.</summary>
    public static class AddRoiRules
    {
        public const string BuildFirstHint = "Build the template first.";
        public const string NameHint = "Enter the new ROI's name, choose its ontology code and type, then press Add ROI.";
        public const string CodeHint = "Choose an ontology code (type in Search ontology to narrow the list). Every ROI needs one: generated RTs leave out ROIs without a code.";
        public const string TypeHint = "Choose an ROI type.";

        /// <param name="templateBuilt">Whether the template has been built (saved once), so ROIs can be added to it.</param>
        /// <param name="name">The name typed for the new ROI.</param>
        /// <param name="rois">The template's ROIs, for the duplicate-name check.</param>
        /// <param name="hasCode">Whether an ontology code is chosen.</param>
        /// <param name="hasType">Whether an ROI type is chosen.</param>
        public static AddRoiState Decide(bool templateBuilt, string? name, IEnumerable<ROIClass> rois, bool hasCode, bool hasType)
        {
            ArgumentNullException.ThrowIfNull(rois);
            if (!templateBuilt)
            {
                return new AddRoiState(false, null, null, BuildFirstHint);
            }

            string text = name ?? string.Empty;
            if (text.Length == 0)
            {
                return new AddRoiState(false, null, null, NameHint);
            }

            string? problem = NameRules.RoiNameProblem(text, rois, null);
            if (problem != null)
            {
                // A name that cannot be used is shown as the problem; the steps after it wait until it is fixed.
                return new AddRoiState(false, problem, null, null);
            }

            string? warning = NameRules.RoiNameWarning(text);
            if (!hasCode)
            {
                return new AddRoiState(false, null, warning, CodeHint);
            }

            if (!hasType)
            {
                return new AddRoiState(false, null, warning, TypeHint);
            }

            return new AddRoiState(true, null, warning, null);
        }
    }
}
