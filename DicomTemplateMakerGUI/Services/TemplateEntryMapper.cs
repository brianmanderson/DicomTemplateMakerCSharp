using System;
using System.Collections.Generic;
using ROIOntologyClass;
using TemplateSync.Model;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Converts between Airtable template entries and the program's ROI classes. Reading follows the
    /// rules of the former ReadAirTable.add_roi; writing follows the former AirTableEntry(ROIClass)
    /// constructor.
    /// </summary>
    internal static class TemplateEntryMapper
    {
        private const byte FallbackR = 255;
        private const byte FallbackG = 0;
        private const byte FallbackB = 0;

        /// <summary>
        /// Builds the ROI for one site. Returns null (with a warning) only when the entry has no
        /// usable name. A missing or malformed colour no longer drops the ROI silently: it is kept
        /// in red and reported.
        /// </summary>
        public static ROIWrapper ToRoiWrapper(SiteRoi siteRoi, ICollection<string> warnings)
        {
            AirTableEntry entry = siteRoi.Entry;
            if (string.IsNullOrWhiteSpace(entry.Structure))
            {
                warnings.Add("Skipped an Airtable record with an empty Structure name.");
                return null;
            }

            string codeMeaning = entry.CommonName ?? entry.Structure;
            if (OntologyCodeClass.RemoveIllegalCharacters(codeMeaning).Length == 0 && entry.SchemeCode == null)
            {
                warnings.Add(entry.Structure + ": skipped because its name has no usable characters and it has no code.");
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
                    warnings.Add(entry.Structure + ": no valid colour in the table, so it is shown in red.");
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
                        warnings.Add(entry.Structure + ": DVH colour '" + entry.DVH_Color + "' is not valid; using the ROI colour.");
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
                return new ROIWrapper(roi, entry.Structure, entry.TG_263R, entry.TG_263Spanish, entry.TG_263SpanishR, entry.TG_263French, entry.TG_263FrenchR);
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                warnings.Add(entry.Structure + ": skipped because the record could not be read (" + ex.Message + ").");
                return null;
            }
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
