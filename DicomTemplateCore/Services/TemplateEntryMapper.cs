using System;
using System.Collections.Generic;
using System.Linq;
using DicomTemplateMakerGUI.DicomTemplateServices;
using ROIOntologyClass;
using TemplateSync.Model;
using TemplateSync.Sync;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>Why <see cref="TemplateEntryMapper.ToRoiWrapper(SiteRoi, Action{TemplateEntryProblem, string})"/> reported an entry.</summary>
    public enum TemplateEntryProblem
    {
        /// <summary>The record has no Structure name; it is skipped.</summary>
        EmptyName,

        /// <summary>The name has no usable characters and there is no code; it is skipped.</summary>
        NoUsableCharacters,

        /// <summary>No valid colour; the ROI is kept in red.</summary>
        RedFallbackColour,

        /// <summary>The DVH colour does not parse; the ROI colour is used.</summary>
        BadDvhColour,

        /// <summary>The code lacks a value or a scheme; generated RTs leave the ROI out.</summary>
        IncompleteCode,

        /// <summary>A value of the record could not be read; it is skipped.</summary>
        UnreadableRecord,
    }

    /// <summary>
    /// Converts between Airtable template entries and the program's ROI classes. Reading follows the
    /// rules of the former ReadAirTable.add_roi; writing follows the former AirTableEntry(ROIClass)
    /// constructor.
    /// </summary>
    public static class TemplateEntryMapper
    {
        private const byte FallbackR = 255;
        private const byte FallbackG = 0;
        private const byte FallbackB = 0;

        /// <summary>
        /// Builds the ROI for one site. Returns null (with a warning) only when the entry has no
        /// usable name. A missing or malformed colour no longer drops the ROI silently: it is kept
        /// in red and reported. An ROI without a complete code (code value and coding scheme) is kept
        /// with a warning that generated RTs leave it out.
        /// </summary>
        public static ROIWrapper? ToRoiWrapper(SiteRoi siteRoi, ICollection<string> warnings)
        {
            return ToRoiWrapper(siteRoi, (_, message) => warnings.Add(message));
        }

        /// <summary>
        /// As <see cref="ToRoiWrapper(SiteRoi, ICollection{string})"/>, but each warning is reported with its kind.
        /// </summary>
        public static ROIWrapper? ToRoiWrapper(SiteRoi siteRoi, Action<TemplateEntryProblem, string> report)
        {
            AirTableEntry entry = siteRoi.Entry;
            if (string.IsNullOrWhiteSpace(entry.Structure))
            {
                report(TemplateEntryProblem.EmptyName, "Skipped an Airtable record with an empty Structure name.");
                return null;
            }

            string codeMeaning = entry.CommonName ?? entry.Structure;
            if (OntologyCodeClass.RemoveIllegalCharacters(codeMeaning).Length == 0 && entry.SchemeCode == null)
            {
                report(TemplateEntryProblem.NoUsableCharacters, entry.Structure + ": skipped because its name has no usable characters and it has no code.");
                return null;
            }

            try
            {
                OntologyCodeClass ontology = new OntologyCodeClass(
                    name: codeMeaning,
                    code_value: entry.SchemeCode,
                    scheme_designated: entry.Scheme,
                    group_version: entry.ContextGroupVersion,
                    mapping_resource: entry.MappingResource,
                    context_identifier: entry.ContextIdentifier,
                    mapping_resource_name: entry.MappingResourceName,
                    mapping_resource_uid: entry.MappingResourceUID,
                    context_uid: entry.ContextUID);

                byte r, g, b;
                if (!entry.TryGetRgb(out r, out g, out b))
                {
                    r = FallbackR;
                    g = FallbackG;
                    b = FallbackB;
                    report(TemplateEntryProblem.RedFallbackColour, entry.Structure + ": no valid colour in the table, so it is shown in red.");
                }

                ROIClass roi = new ROIClass(r, g, b, entry.Structure, entry.Type, ontology);
                if (entry.DVH_Color != null)
                {
                    roi.DVHLineColor = entry.DVH_Color;
                    try
                    {
                        roi.build_dvh_line_color();
                    }
                    catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                    {
                        roi.DVHLineColor = new ROIClass().DVHLineColor;
                        roi.build_dvh_line_color();
                        report(TemplateEntryProblem.BadDvhColour, entry.Structure + ": DVH colour '" + entry.DVH_Color + "' is not valid; using the ROI colour.");
                    }
                }

                if (entry.DVH_Style != null)
                {
                    roi.DVHLineStyle = entry.DVH_Style;
                }

                if (entry.DVH_Width != null)
                {
                    roi.DVHLineWidth = entry.DVH_Width;
                }

                roi.Include = siteRoi.Include;
                string? missing = RtStructureBuilder.MissingCodeParts(ontology);
                if (missing != null)
                {
                    report(TemplateEntryProblem.IncompleteCode, entry.Structure + ": its code has no " + missing + " in the table, so generated RTs leave it out until a complete code is chosen for it.");
                }

                return new ROIWrapper(roi, entry.Structure, entry.TG_263R, entry.TG_263Spanish, entry.TG_263SpanishR, entry.TG_263French, entry.TG_263FrenchR);
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                report(TemplateEntryProblem.UnreadableRecord, entry.Structure + ": skipped because the record could not be read (" + ex.Message + ").");
                return null;
            }
        }

        /// <summary>
        /// One site's write: every ROI's published values (<see cref="ToDesiredEntry"/>), and its Include flag choosing
        /// Template_Recommend or Template_Consider. The windows and the evaluation tool's write-airtable command both
        /// build their writes here, so they cannot send different records for the same template.
        /// </summary>
        public static TemplateWrite ToTemplateWrite(string site, IEnumerable<ROIClass> rois)
        {
            if (rois == null)
            {
                throw new ArgumentNullException(nameof(rois));
            }

            return new TemplateWrite(site, rois.Select(roi => new LocalRoi(ToDesiredEntry(roi), roi.Include)).ToList());
        }

        /// <summary>The values an ROI publishes to Airtable. Site lists are filled in by the write planner.</summary>
        public static AirTableEntry ToDesiredEntry(ROIClass roi)
        {
            if (roi == null)
            {
                throw new ArgumentNullException(nameof(roi));
            }

            OntologyCodeClass ontology = roi.Ontology_Class ?? new OntologyCodeClass();
            return new AirTableEntry
            {
                Scheme = ontology.Scheme,
                SchemeCode = ontology.CodeValue,
                CommonName = ontology.CodeMeaning,
                ContextGroupVersion = ontology.ContextGroupVersion,
                MappingResource = ontology.MappingResource,
                ContextIdentifier = ontology.ContextIdentifier,
                MappingResourceName = ontology.MappingResourceName,
                MappingResourceUID = ontology.MappingResourceUID,
                ContextUID = ontology.ContextUID,
                Structure = roi.ROIName,
                Type = roi.ROI_Interpreted_type,
                Colors_RGB = new List<string> { "Auto:" + roi.R + "," + roi.G + "," + roi.B },
                RGB = roi.R + "," + roi.G + "," + roi.B,
                DVH_Color = roi.DVHLineColor,
                DVH_Style = roi.DVHLineStyle,
                DVH_Width = roi.DVHLineWidth,
                DVH_Type_Index = roi.TypeIndex,
                DVH_ContourStyle = roi.ContourStyle,
            };
        }
    }
}
